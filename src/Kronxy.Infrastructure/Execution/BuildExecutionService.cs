using System.Text;
using System.Text.Json;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Execution;

namespace Kronxy.Infrastructure.Execution;

public sealed class BuildExecutionService :
    IBuildExecutionService
{
    private static readonly JsonSerializerOptions
        ReportJsonOptions =
            new()
            {
                WriteIndented = true
            };

    private readonly ISecureToolExecutor secureToolExecutor;
    private readonly IArtifactStore artifactStore;

    public BuildExecutionService(
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

    public async Task<BuildExecutionResult> ExecuteAsync(
        BuildExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidRequest(request))
        {
            return BuildExecutionResult.Failure(
                BuildExecutionFailureKind.InvalidRequest,
                "BUILD_INVALID_REQUEST");
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
                                    .DotnetBuild,

                            Target =
                                request.Target,

                            CorrelationId =
                                request.CorrelationId
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

            BuildExecutionReport report =
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
                        request.IsHumanReviewCorrection ||
                        request.IsGovernedHumanCorrection
                            ? ArtifactType.BuildHumanReviewCorrectionStandardOutput
                            : request.IsBuildCorrectionRetry
                            ? ArtifactType.BuildCorrectionRetryStandardOutput
                            : request.IsBuildCorrection
                            ? ArtifactType.BuildCorrectionStandardOutput
                            : ArtifactType.BuildStandardOutput,
                        Encoding.UTF8.GetBytes(
                            toolResult.StandardOutput),
                        cancellationToken)
                    .ConfigureAwait(false);

            ArtifactWriteResult stderrWrite =
                await WriteArtifactAsync(
                        request,
                        request.IsHumanReviewCorrection ||
                        request.IsGovernedHumanCorrection
                            ? ArtifactType.BuildHumanReviewCorrectionStandardError
                            : request.IsBuildCorrectionRetry
                            ? ArtifactType.BuildCorrectionRetryStandardError
                            : request.IsBuildCorrection
                            ? ArtifactType.BuildCorrectionStandardError
                            : ArtifactType.BuildStandardError,
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
                        request.IsHumanReviewCorrection ||
                        request.IsGovernedHumanCorrection
                            ? ArtifactType.BuildHumanReviewCorrectionReport
                            : request.IsBuildCorrectionRetry
                            ? ArtifactType.BuildCorrectionRetryReport
                            : request.IsBuildCorrection
                            ? ArtifactType.BuildCorrectionReport
                            : ArtifactType.BuildReport,
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
                BuildExecutionFailureKind kind =
                    artifactFailure.FailureKind ==
                    ArtifactStoreFailureKind.Cancelled
                        ? BuildExecutionFailureKind
                            .Cancelled
                        : BuildExecutionFailureKind
                            .ArtifactWriteFailure;

                return BuildExecutionResult.Failure(
                    kind,
                    string.IsNullOrWhiteSpace(
                        artifactFailure.ErrorCode)
                        ? "BUILD_ARTIFACT_WRITE_FAILED"
                        : artifactFailure.ErrorCode,
                    report,
                    reportArtifact,
                    stdoutArtifact,
                    stderrArtifact);
            }

            BuildExecutionFailureKind toolFailure =
                MapToolFailure(
                    toolResult);

            if (toolFailure !=
                BuildExecutionFailureKind.None)
            {
                return BuildExecutionResult.Failure(
                    toolFailure,
                    GetToolErrorCode(
                        toolResult),
                    report,
                    reportArtifact,
                    stdoutArtifact,
                    stderrArtifact);
            }

            return BuildExecutionResult.Success(
                report,
                reportArtifact!,
                stdoutArtifact!,
                stderrArtifact!);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            return BuildExecutionResult.Failure(
                BuildExecutionFailureKind.Cancelled,
                "BUILD_CANCELLED");
        }
        catch
        {
            return BuildExecutionResult.Failure(
                BuildExecutionFailureKind.InternalFailure,
                "BUILD_INTERNAL_FAILURE");
        }
    }

    private async Task<ArtifactWriteResult>
        WriteArtifactAsync(
            BuildExecutionRequest request,
            ArtifactType artifactType,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken)
    {
        return await artifactStore
            .WriteAsync(
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
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static ArtifactRecord?
        SuccessfulArtifact(
            ArtifactWriteResult result)
    {
        return result.IsSuccess
            ? result.Artifact
            : null;
    }

    private static ArtifactWriteResult?
        FirstFailure(
            params ArtifactWriteResult[] results)
    {
        foreach (ArtifactWriteResult result
                 in results)
        {
            if (!result.IsSuccess)
            {
                return result;
            }
        }

        return null;
    }

    private static BuildExecutionFailureKind
        MapToolFailure(
            SecureToolResult result)
    {
        return result.Outcome switch
        {
            ToolExecutionOutcome.Completed
                when result.ExitCode == 0 =>
                    BuildExecutionFailureKind.None,

            ToolExecutionOutcome.Completed =>
                BuildExecutionFailureKind.BuildFailed,

            ToolExecutionOutcome.NonZeroExitCode =>
                BuildExecutionFailureKind.BuildFailed,

            ToolExecutionOutcome.Rejected =>
                BuildExecutionFailureKind.ToolRejected,

            ToolExecutionOutcome.TimedOut =>
                BuildExecutionFailureKind.TimedOut,

            ToolExecutionOutcome.Cancelled =>
                BuildExecutionFailureKind.Cancelled,

            ToolExecutionOutcome.OutputLimitExceeded =>
                BuildExecutionFailureKind.OutputLimitExceeded,

            ToolExecutionOutcome.StartFailure =>
                BuildExecutionFailureKind.StartFailure,

            _ =>
                BuildExecutionFailureKind.InternalFailure
        };
    }

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
                "BUILD_FAILED",

            ToolExecutionOutcome.Completed =>
                "BUILD_FAILED",

            ToolExecutionOutcome.Rejected =>
                "BUILD_TOOL_REJECTED",

            ToolExecutionOutcome.TimedOut =>
                "BUILD_TIMED_OUT",

            ToolExecutionOutcome.Cancelled =>
                "BUILD_CANCELLED",

            ToolExecutionOutcome.OutputLimitExceeded =>
                "BUILD_OUTPUT_LIMIT_EXCEEDED",

            ToolExecutionOutcome.StartFailure =>
                "BUILD_START_FAILURE",

            _ =>
                "BUILD_INTERNAL_FAILURE"
        };
    }

    private static bool IsValidRequest(
        BuildExecutionRequest request)
    {
        return request is not null &&
               request.JobId != Guid.Empty &&
               request.RunId != Guid.Empty &&
               request.Repository is not null &&
               request.Repository.JobId ==
                   request.JobId &&
               !string.IsNullOrWhiteSpace(
                   request.Target) &&
               request.CorrelationId.IndexOfAny(
                   ['\0', '\r', '\n']) < 0;
    }
}
