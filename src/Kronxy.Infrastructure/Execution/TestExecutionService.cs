using System.Text;
using System.Text.Json;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Execution;
using Kronxy.Infrastructure.Artifacts;

namespace Kronxy.Infrastructure.Execution;

public sealed class TestExecutionService :
    ITestExecutionService
{
    private static readonly JsonSerializerOptions
        ReportJsonOptions =
            new()
            {
                WriteIndented = true
            };

    private readonly ISecureToolExecutor secureToolExecutor;
    private readonly IArtifactStore artifactStore;
    private readonly ArtifactStoreOptions artifactStoreOptions;

    public TestExecutionService(
        ISecureToolExecutor secureToolExecutor,
        IArtifactStore artifactStore,
        ArtifactStoreOptions artifactStoreOptions)
    {
        this.secureToolExecutor =
            secureToolExecutor ??
            throw new ArgumentNullException(
                nameof(secureToolExecutor));

        this.artifactStore =
            artifactStore ??
            throw new ArgumentNullException(
                nameof(artifactStore));

        this.artifactStoreOptions =
            artifactStoreOptions ??
            throw new ArgumentNullException(
                nameof(artifactStoreOptions));

        this.artifactStoreOptions.Validate();
    }

    public async Task<TestExecutionResult> ExecuteAsync(
        TestExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidRequest(request))
        {
            return TestExecutionResult.Failure(
                TestExecutionFailureKind.InvalidRequest,
                "TEST_INVALID_REQUEST");
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
                                SecureToolOperation.DotnetTest,

                            Target =
                                request.Target,

                            CorrelationId =
                                request.CorrelationId
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

            TestExecutionReport report =
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
                        ArtifactType.TestStandardOutput,
                        Encoding.UTF8.GetBytes(
                            toolResult.StandardOutput),
                        cancellationToken)
                    .ConfigureAwait(false);

            ArtifactWriteResult stderrWrite =
                await WriteArtifactAsync(
                        request,
                        ArtifactType.TestStandardError,
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
                        ArtifactType.TestReport,
                        reportBytes,
                        cancellationToken)
                    .ConfigureAwait(false);

            ArtifactRecord? stdoutArtifact =
                SuccessfulArtifact(
                    stdoutWrite);

            ArtifactRecord? stderrArtifact =
                SuccessfulArtifact(
                    stderrWrite);

            ArtifactRecord? reportArtifact =
                SuccessfulArtifact(
                    reportWrite);

            ArtifactWriteResult? evidenceFailure =
                FirstFailure(
                    stdoutWrite,
                    stderrWrite,
                    reportWrite);

            if (evidenceFailure is not null)
            {
                return ArtifactFailure(
                    evidenceFailure,
                    report,
                    reportArtifact,
                    null,
                    stdoutArtifact,
                    stderrArtifact);
            }

            TestExecutionFailureKind toolFailure =
                MapToolFailure(
                    toolResult);

            bool executionProducedTestResults =
                toolResult.Outcome is
                    ToolExecutionOutcome.Completed or
                    ToolExecutionOutcome.NonZeroExitCode;

            if (!executionProducedTestResults)
            {
                return TestExecutionResult.Failure(
                    toolFailure,
                    GetToolErrorCode(
                        toolResult),
                    report,
                    reportArtifact,
                    null,
                    stdoutArtifact,
                    stderrArtifact);
            }

            TestResultsReadResult trx =
                await ReadTestResultsAsync(
                        request,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!trx.IsSuccess)
            {
                return TestExecutionResult.Failure(
                    trx.FailureKind,
                    trx.ErrorCode,
                    report,
                    reportArtifact,
                    null,
                    stdoutArtifact,
                    stderrArtifact);
            }

            ArtifactWriteResult resultsWrite =
                await WriteArtifactAsync(
                        request,
                        ArtifactType.TestResults,
                        trx.Content!,
                        cancellationToken)
                    .ConfigureAwait(false);

            ArtifactRecord? resultsArtifact =
                SuccessfulArtifact(
                    resultsWrite);

            if (!resultsWrite.IsSuccess ||
                resultsArtifact is null)
            {
                return ArtifactFailure(
                    resultsWrite,
                    report,
                    reportArtifact,
                    resultsArtifact,
                    stdoutArtifact,
                    stderrArtifact);
            }

            if (toolFailure !=
                TestExecutionFailureKind.None)
            {
                return TestExecutionResult.Failure(
                    toolFailure,
                    GetToolErrorCode(
                        toolResult),
                    report,
                    reportArtifact,
                    resultsArtifact,
                    stdoutArtifact,
                    stderrArtifact);
            }

            return TestExecutionResult.Success(
                report,
                reportArtifact!,
                resultsArtifact,
                stdoutArtifact!,
                stderrArtifact!);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            return TestExecutionResult.Failure(
                TestExecutionFailureKind.Cancelled,
                "TEST_CANCELLED");
        }
        catch (Exception exception)
            when (exception is not
                OutOfMemoryException and not
                StackOverflowException)
        {
            return TestExecutionResult.Failure(
                TestExecutionFailureKind.InternalFailure,
                "TEST_INTERNAL_FAILURE");
        }
    }

    private async Task<TestResultsReadResult>
        ReadTestResultsAsync(
            TestExecutionRequest request,
            CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            string workspace =
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(
                        request.Repository
                            .WorkspacePath));

            string kronxyDirectory =
                Path.GetFullPath(
                    Path.Combine(
                        workspace,
                        ".kronxy"));

            string resultsDirectory =
                Path.GetFullPath(
                    Path.Combine(
                        kronxyDirectory,
                        "test-results"));

            string trxPath =
                Path.GetFullPath(
                    Path.Combine(
                        resultsDirectory,
                        SecureToolExecutor
                            .TestResultsFileName));

            if (!IsUnderRoot(
                    workspace,
                    kronxyDirectory) ||
                !IsUnderRoot(
                    workspace,
                    resultsDirectory) ||
                !IsUnderRoot(
                    workspace,
                    trxPath))
            {
                return TestResultsReadResult.Failure(
                    TestExecutionFailureKind
                        .TestResultsUnsafe,
                    "TEST_RESULTS_PATH_UNSAFE");
            }

            DirectoryInfo kronxyInfo =
                new(
                    kronxyDirectory);

            DirectoryInfo resultsInfo =
                new(
                    resultsDirectory);

            FileInfo trxInfo =
                new(
                    trxPath);

            if (!kronxyInfo.Exists ||
                !resultsInfo.Exists ||
                !trxInfo.Exists)
            {
                return TestResultsReadResult.Failure(
                    TestExecutionFailureKind
                        .TestResultsMissing,
                    "TEST_RESULTS_MISSING");
            }

            if (IsLink(kronxyInfo) ||
                IsLink(resultsInfo) ||
                IsLink(trxInfo))
            {
                return TestResultsReadResult.Failure(
                    TestExecutionFailureKind
                        .TestResultsUnsafe,
                    "TEST_RESULTS_PATH_UNSAFE");
            }

            trxInfo.Refresh();

            if (trxInfo.Length < 0 ||
                trxInfo.Length >
                    artifactStoreOptions
                        .MaxArtifactBytes ||
                trxInfo.Length >
                    int.MaxValue)
            {
                return TestResultsReadResult.Failure(
                    TestExecutionFailureKind
                        .TestResultsUnsafe,
                    "TEST_RESULTS_TOO_LARGE");
            }

            byte[] content =
                await File.ReadAllBytesAsync(
                        trxPath,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (content.LongLength !=
                trxInfo.Length)
            {
                return TestResultsReadResult.Failure(
                    TestExecutionFailureKind
                        .TestResultsUnsafe,
                    "TEST_RESULTS_CHANGED_DURING_READ");
            }

            FileInfo afterRead =
                new(
                    trxPath);

            afterRead.Refresh();

            if (!afterRead.Exists ||
                IsLink(afterRead) ||
                afterRead.Length !=
                    content.LongLength)
            {
                return TestResultsReadResult.Failure(
                    TestExecutionFailureKind
                        .TestResultsUnsafe,
                    "TEST_RESULTS_CHANGED_DURING_READ");
            }

            return TestResultsReadResult.Success(
                content);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            throw;
        }
        catch (
            Exception exception)
            when (
                exception is
                    IOException or
                    UnauthorizedAccessException or
                    ArgumentException or
                    NotSupportedException)
        {
            return TestResultsReadResult.Failure(
                TestExecutionFailureKind
                    .TestResultsUnsafe,
                "TEST_RESULTS_READ_FAILED");
        }
    }

    private Task<ArtifactWriteResult>
        WriteArtifactAsync(
            TestExecutionRequest request,
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

    private static TestExecutionResult
        ArtifactFailure(
            ArtifactWriteResult failure,
            TestExecutionReport report,
            ArtifactRecord? reportArtifact,
            ArtifactRecord? resultsArtifact,
            ArtifactRecord? stdoutArtifact,
            ArtifactRecord? stderrArtifact)
    {
        TestExecutionFailureKind kind =
            failure.FailureKind ==
            ArtifactStoreFailureKind.Cancelled
                ? TestExecutionFailureKind
                    .Cancelled
                : TestExecutionFailureKind
                    .ArtifactWriteFailure;

        string errorCode =
            string.IsNullOrWhiteSpace(
                failure.ErrorCode)
                ? "TEST_ARTIFACT_WRITE_FAILED"
                : failure.ErrorCode;

        return TestExecutionResult.Failure(
            kind,
            errorCode,
            report,
            reportArtifact,
            resultsArtifact,
            stdoutArtifact,
            stderrArtifact);
    }

    private static bool IsValidRequest(
        TestExecutionRequest? request)
    {
        if (request is null ||
            request.JobId == Guid.Empty ||
            request.RunId == Guid.Empty ||
            request.Repository is null ||
            request.Repository.JobId !=
                request.JobId ||
            string.IsNullOrWhiteSpace(
                request.Target))
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

    private static TestExecutionFailureKind
        MapToolFailure(
            SecureToolResult result) =>
        result.Outcome switch
        {
            ToolExecutionOutcome.Completed
                when result.ExitCode == 0 =>
                    TestExecutionFailureKind.None,

            ToolExecutionOutcome.Completed =>
                TestExecutionFailureKind
                    .TestsFailed,

            ToolExecutionOutcome.NonZeroExitCode =>
                TestExecutionFailureKind
                    .TestsFailed,

            ToolExecutionOutcome.Rejected =>
                TestExecutionFailureKind
                    .ToolRejected,

            ToolExecutionOutcome.TimedOut =>
                TestExecutionFailureKind
                    .TimedOut,

            ToolExecutionOutcome.Cancelled =>
                TestExecutionFailureKind
                    .Cancelled,

            ToolExecutionOutcome.OutputLimitExceeded =>
                TestExecutionFailureKind
                    .OutputLimitExceeded,

            ToolExecutionOutcome.StartFailure =>
                TestExecutionFailureKind
                    .StartFailure,

            _ =>
                TestExecutionFailureKind
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
            ToolExecutionOutcome.Completed
                when result.ExitCode != 0 =>
                    "TESTS_FAILED",

            ToolExecutionOutcome.NonZeroExitCode =>
                "TESTS_FAILED",

            ToolExecutionOutcome.Rejected =>
                "TEST_TOOL_REJECTED",

            ToolExecutionOutcome.TimedOut =>
                "TEST_TIMED_OUT",

            ToolExecutionOutcome.Cancelled =>
                "TEST_CANCELLED",

            ToolExecutionOutcome.OutputLimitExceeded =>
                "TEST_OUTPUT_LIMIT_EXCEEDED",

            ToolExecutionOutcome.StartFailure =>
                "TEST_START_FAILURE",

            _ =>
                "TEST_INTERNAL_FAILURE"
        };
    }

    private static bool IsUnderRoot(
        string root,
        string candidate)
    {
        string normalizedRoot =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(root));

        string normalizedCandidate =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(candidate));

        if (string.Equals(
                normalizedRoot,
                normalizedCandidate,
                StringComparison.Ordinal))
        {
            return true;
        }

        string prefix =
            normalizedRoot +
            Path.DirectorySeparatorChar;

        return normalizedCandidate.StartsWith(
            prefix,
            StringComparison.Ordinal);
    }

    private static bool IsLink(
        FileSystemInfo info)
    {
        try
        {
            info.Refresh();

            return info.LinkTarget is not null ||
                   (info.Attributes &
                    FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return true;
        }
    }

    private sealed record TestResultsReadResult(
        byte[]? Content,
        TestExecutionFailureKind FailureKind,
        string ErrorCode)
    {
        public bool IsSuccess =>
            FailureKind ==
                TestExecutionFailureKind.None &&
            Content is not null;

        public static TestResultsReadResult Success(
            byte[] content) =>
            new(
                content,
                TestExecutionFailureKind.None,
                string.Empty);

        public static TestResultsReadResult Failure(
            TestExecutionFailureKind failureKind,
            string errorCode) =>
            new(
                null,
                failureKind,
                errorCode);
    }
}
