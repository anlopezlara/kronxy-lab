using Kronxy.Application.Artifacts;
using Kronxy.Application.Repositories;

namespace Kronxy.Application.Execution;

public enum TestExecutionFailureKind
{
    None = 0,
    InvalidRequest = 10,
    ToolRejected = 20,
    TestsFailed = 30,
    TimedOut = 40,
    Cancelled = 50,
    OutputLimitExceeded = 60,
    StartFailure = 70,
    TestResultsMissing = 80,
    TestResultsUnsafe = 90,
    ArtifactWriteFailure = 100,
    InternalFailure = 110
}

public sealed record TestExecutionRequest
{
    public required Guid JobId { get; init; }

    public required Guid RunId { get; init; }

    public required RepositoryWorktreeHandle Repository { get; init; }

    public required string Target { get; init; }

    public string CorrelationId { get; init; } = string.Empty;
}

public sealed record TestExecutionReport(
    Guid JobId,
    Guid RunId,
    string Target,
    ToolExecutionOutcome Outcome,
    int? ExitCode,
    string ErrorCode,
    DateTime StartedOnUtc,
    DateTime EndedOnUtc,
    TimeSpan Duration)
{
    public bool IsSuccess =>
        Outcome == ToolExecutionOutcome.Completed &&
        ExitCode == 0;
}

public sealed record TestExecutionResult(
    TestExecutionReport? Report,
    ArtifactRecord? ReportArtifact,
    ArtifactRecord? ResultsArtifact,
    ArtifactRecord? StandardOutputArtifact,
    ArtifactRecord? StandardErrorArtifact,
    TestExecutionFailureKind FailureKind,
    string ErrorCode)
{
    public bool IsSuccess =>
        FailureKind == TestExecutionFailureKind.None &&
        Report is not null &&
        Report.IsSuccess &&
        ReportArtifact is not null &&
        ResultsArtifact is not null &&
        StandardOutputArtifact is not null &&
        StandardErrorArtifact is not null;

    public static TestExecutionResult Success(
        TestExecutionReport report,
        ArtifactRecord reportArtifact,
        ArtifactRecord resultsArtifact,
        ArtifactRecord standardOutputArtifact,
        ArtifactRecord standardErrorArtifact) =>
        new(
            report,
            reportArtifact,
            resultsArtifact,
            standardOutputArtifact,
            standardErrorArtifact,
            TestExecutionFailureKind.None,
            string.Empty);

    public static TestExecutionResult Failure(
        TestExecutionFailureKind failureKind,
        string errorCode,
        TestExecutionReport? report = null,
        ArtifactRecord? reportArtifact = null,
        ArtifactRecord? resultsArtifact = null,
        ArtifactRecord? standardOutputArtifact = null,
        ArtifactRecord? standardErrorArtifact = null) =>
        new(
            report,
            reportArtifact,
            resultsArtifact,
            standardOutputArtifact,
            standardErrorArtifact,
            failureKind,
            errorCode);
}

public interface ITestExecutionService
{
    Task<TestExecutionResult> ExecuteAsync(
        TestExecutionRequest request,
        CancellationToken cancellationToken = default);
}
