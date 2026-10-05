using Kronxy.Application.Artifacts;
using Kronxy.Application.Repositories;

namespace Kronxy.Application.Execution;

public enum BuildExecutionFailureKind
{
    None = 0,
    InvalidRequest = 10,
    ToolRejected = 20,
    BuildFailed = 30,
    TimedOut = 40,
    Cancelled = 50,
    OutputLimitExceeded = 60,
    StartFailure = 70,
    ArtifactWriteFailure = 80,
    InternalFailure = 90
}

public sealed record BuildExecutionRequest
{
    public required Guid JobId { get; init; }

    public required Guid RunId { get; init; }

    public required RepositoryWorktreeHandle Repository
    {
        get;
        init;
    }

    public required string Target { get; init; }

    public string CorrelationId { get; init; } =
        string.Empty;

    public bool IsBuildCorrection { get; init; }
    public bool IsBuildCorrectionRetry { get; init; }
    public bool IsHumanReviewCorrection { get; init; }
    public bool IsGovernedHumanCorrection { get; init; }
}

public sealed record BuildExecutionReport(
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

public sealed record BuildExecutionResult(
    BuildExecutionReport? Report,
    ArtifactRecord? ReportArtifact,
    ArtifactRecord? StandardOutputArtifact,
    ArtifactRecord? StandardErrorArtifact,
    BuildExecutionFailureKind FailureKind,
    string ErrorCode)
{
    public bool IsSuccess =>
        FailureKind == BuildExecutionFailureKind.None &&
        Report is not null &&
        Report.IsSuccess &&
        ReportArtifact is not null &&
        StandardOutputArtifact is not null &&
        StandardErrorArtifact is not null;

    public static BuildExecutionResult Success(
        BuildExecutionReport report,
        ArtifactRecord reportArtifact,
        ArtifactRecord standardOutputArtifact,
        ArtifactRecord standardErrorArtifact) =>
        new(
            report,
            reportArtifact,
            standardOutputArtifact,
            standardErrorArtifact,
            BuildExecutionFailureKind.None,
            string.Empty);

    public static BuildExecutionResult Failure(
        BuildExecutionFailureKind failureKind,
        string errorCode,
        BuildExecutionReport? report = null,
        ArtifactRecord? reportArtifact = null,
        ArtifactRecord? standardOutputArtifact = null,
        ArtifactRecord? standardErrorArtifact = null) =>
        new(
            report,
            reportArtifact,
            standardOutputArtifact,
            standardErrorArtifact,
            failureKind,
            errorCode);
}

public interface IBuildExecutionService
{
    Task<BuildExecutionResult> ExecuteAsync(
        BuildExecutionRequest request,
        CancellationToken cancellationToken = default);
}
