using System.Text;
using System.Text.Json;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Execution;

namespace Kronxy.Infrastructure.Execution;

public sealed class RestoreExecutionService :
    IRestoreExecutionService
{
    private static readonly JsonSerializerOptions
        ReportJsonOptions =
            new(JsonSerializerDefaults.Web)
            {
                WriteIndented = true
            };

    private readonly ISecureToolExecutor secureToolExecutor;
    private readonly IArtifactStore artifactStore;

    public RestoreExecutionService(
        ISecureToolExecutor secureToolExecutor,
        IArtifactStore artifactStore)
    {
        this.secureToolExecutor =
            secureToolExecutor ??
            throw new ArgumentNullException(
                nameof(secureToolExecutor));

        this.artifactStore =
            artifactStore ??
            throw new ArgumentNullException(
                nameof(artifactStore));
    }

    public async Task<RestoreExecutionResult> ExecuteAsync(
        RestoreExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidRequest(request))
        {
            return RestoreExecutionResult.Failure(
                RestoreExecutionFailureKind.InvalidRequest,
                "RESTORE_INVALID_REQUEST");
        }

        try
        {
            SecureToolResult toolResult =
                await secureToolExecutor
                    .ExecuteAsync(
                        new SecureToolRequest
                        {
                            Repository =
                                request.Repository,

                            Operation =
                                SecureToolOperation
                                    .DotnetRestore,

                            Target =
                                request.Target,

                            CorrelationId =
                                request.CorrelationId
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

            RestoreExecutionReport report =
                new(
                    request.JobId,
                    request.RunId,
                    request.Target,
                    toolResult.Outcome,
                    toolResult.ExitCode,
                    toolResult.ErrorCode,
                    toolResult.Audit.StartedOnUtc,
                    toolResult.Audit.EndedOnUtc,
                    toolResult.Audit.Duration);

            ArtifactWriteResult stdoutWrite =
                await WriteArtifactAsync(
                        request,
                        ArtifactType
                            .RestoreStandardOutput,
                        Encoding.UTF8.GetBytes(
                            toolResult.StandardOutput),
                        cancellationToken)
                    .ConfigureAwait(false);

            ArtifactWriteResult stderrWrite =
                await WriteArtifactAsync(
                        request,
                        ArtifactType
                            .RestoreStandardError,
                        Encoding.UTF8.GetBytes(
                            toolResult.StandardError),
                        cancellationToken)
                    .ConfigureAwait(false);

            byte[] reportBytes =
                JsonSerializer.SerializeToUtf8Bytes(
                    report,
                    ReportJsonOptions);

            ArtifactWriteResult reportWrite =
                await WriteArtifactAsync(
                        request,
                        ArtifactType.RestoreReport,
                        reportBytes,
                        cancellationToken)
                    .ConfigureAwait(false);

            ArtifactRecord? stdoutArtifact =
                SuccessfulArtifact(stdoutWrite);

            ArtifactRecord? stderrArtifact =
                SuccessfulArtifact(stderrWrite);

            ArtifactRecord? reportArtifact =
                SuccessfulArtifact(reportWrite);

            ArtifactWriteResult? artifactFailure =
                FirstFailure(
                    stdoutWrite,
                    stderrWrite,
                    reportWrite);

            if (artifactFailure is not null)
            {
                RestoreExecutionFailureKind kind =
                    artifactFailure.FailureKind ==
                    ArtifactStoreFailureKind.Cancelled
                        ? RestoreExecutionFailureKind
                            .Cancelled
                        : RestoreExecutionFailureKind
                            .ArtifactWriteFailure;

                return RestoreExecutionResult.Failure(
                    kind,
                    string.IsNullOrWhiteSpace(
                        artifactFailure.ErrorCode)
                        ? "RESTORE_ARTIFACT_WRITE_FAILED"
                        : artifactFailure.ErrorCode,
                    report,
                    reportArtifact,
                    stdoutArtifact,
                    stderrArtifact);
            }

            RestoreExecutionFailureKind toolFailure =
                MapToolFailure(
                    toolResult);

            if (toolFailure !=
                RestoreExecutionFailureKind.None)
            {
                return RestoreExecutionResult.Failure(
                    toolFailure,
                    GetToolErrorCode(
                        toolResult),
                    report,
                    reportArtifact,
                    stdoutArtifact,
                    stderrArtifact);
            }

            return RestoreExecutionResult.Success(
                report,
                reportArtifact!,
                stdoutArtifact!,
                stderrArtifact!);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            return RestoreExecutionResult.Failure(
                RestoreExecutionFailureKind.Cancelled,
                "RESTORE_CANCELLED");
        }
        catch (Exception exception)
            when (exception is not
                OutOfMemoryException and not
                StackOverflowException)
        {
            return RestoreExecutionResult.Failure(
                RestoreExecutionFailureKind
                    .InternalFailure,
                "RESTORE_INTERNAL_FAILURE");
        }
    }

    private Task<ArtifactWriteResult>
        WriteArtifactAsync(
            RestoreExecutionRequest request,
            ArtifactType artifactType,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken) =>
        artifactStore.WriteAsync(
            new ArtifactWriteRequest
            {
                JobId =
                    request.JobId,

                RunId =
                    request.RunId,

                ArtifactType =
                    artifactType,

                Content =
                    content,

                CorrelationId =
                    request.CorrelationId
            },
            cancellationToken);

    private static bool IsValidRequest(
        RestoreExecutionRequest? request)
    {
        if (request is null ||
            request.JobId == Guid.Empty ||
            request.RunId == Guid.Empty ||
            request.Repository is null ||
            string.IsNullOrWhiteSpace(
                request.Target))
        {
            return false;
        }

        if (request.Repository.JobId !=
            request.JobId)
        {
            return false;
        }

        return request.CorrelationId
            .IndexOfAny(
                ['\0', '\r', '\n']) < 0;
    }

    private static ArtifactRecord?
        SuccessfulArtifact(
            ArtifactWriteResult result) =>
        result.IsSuccess
            ? result.Artifact
            : null;

    private static ArtifactWriteResult?
        FirstFailure(
            params ArtifactWriteResult[] results) =>
        results.FirstOrDefault(
            result =>
                !result.IsSuccess ||
                result.Artifact is null);

    private static RestoreExecutionFailureKind
        MapToolFailure(
            SecureToolResult result) =>
        result.Outcome switch
        {
            ToolExecutionOutcome.Completed
                when result.ExitCode == 0 =>
                    RestoreExecutionFailureKind.None,

            ToolExecutionOutcome.Completed =>
                RestoreExecutionFailureKind
                    .RestoreFailed,

            ToolExecutionOutcome.NonZeroExitCode =>
                RestoreExecutionFailureKind
                    .RestoreFailed,

            ToolExecutionOutcome.Rejected =>
                RestoreExecutionFailureKind
                    .ToolRejected,

            ToolExecutionOutcome.TimedOut =>
                RestoreExecutionFailureKind
                    .TimedOut,

            ToolExecutionOutcome.Cancelled =>
                RestoreExecutionFailureKind
                    .Cancelled,

            ToolExecutionOutcome.OutputLimitExceeded =>
                RestoreExecutionFailureKind
                    .OutputLimitExceeded,

            ToolExecutionOutcome.StartFailure =>
                RestoreExecutionFailureKind
                    .StartFailure,

            _ =>
                RestoreExecutionFailureKind
                    .InternalFailure
        };

    private static string GetToolErrorCode(
        SecureToolResult result)
    {
        if (!string.IsNullOrWhiteSpace(
                result.ErrorCode))
        {
            return result.ErrorCode;
        }

        return result.Outcome switch
        {
            ToolExecutionOutcome.NonZeroExitCode =>
                "RESTORE_NON_ZERO_EXIT_CODE",

            ToolExecutionOutcome.Rejected =>
                "RESTORE_TOOL_REJECTED",

            ToolExecutionOutcome.TimedOut =>
                "RESTORE_TIMED_OUT",

            ToolExecutionOutcome.Cancelled =>
                "RESTORE_CANCELLED",

            ToolExecutionOutcome.OutputLimitExceeded =>
                "RESTORE_OUTPUT_LIMIT_EXCEEDED",

            ToolExecutionOutcome.StartFailure =>
                "RESTORE_START_FAILURE",

            _ =>
                "RESTORE_FAILED"
        };
    }
}
