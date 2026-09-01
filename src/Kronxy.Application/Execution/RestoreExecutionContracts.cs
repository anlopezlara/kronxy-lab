using Kronxy.Application.Artifacts;
using Kronxy.Application.Repositories;

namespace Kronxy.Application.Execution;

public enum RestoreExecutionFailureKind
{
    None = 0,
    InvalidRequest = 10,
    ToolRejected = 20,
    RestoreFailed = 30,
    TimedOut = 40,
    Cancelled = 50,
    OutputLimitExceeded = 60,
    StartFailure = 70,
    ArtifactWriteFailure = 80,
    InternalFailure = 90
}

public sealed record RestoreExecutionRequest
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
}

public sealed record RestoreExecutionReport(
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

public sealed record RestoreExecutionResult(
    RestoreExecutionReport? Report,
    ArtifactRecord? ReportArtifact,
    ArtifactRecord? StandardOutputArtifact,
    ArtifactRecord? StandardErrorArtifact,
    RestoreExecutionFailureKind FailureKind,
    string ErrorCode)
{
    public bool IsSuccess =>
        FailureKind == RestoreExecutionFailureKind.None &&
        Report is not null &&
        Report.IsSuccess &&
        ReportArtifact is not null &&
        StandardOutputArtifact is not null &&
        StandardErrorArtifact is not null;

    public static RestoreExecutionResult Success(
        RestoreExecutionReport report,
        ArtifactRecord reportArtifact,
        ArtifactRecord standardOutputArtifact,
        ArtifactRecord standardErrorArtifact) =>
        new(
            report,
            reportArtifact,
            standardOutputArtifact,
            standardErrorArtifact,
            RestoreExecutionFailureKind.None,
            string.Empty);

    public static RestoreExecutionResult Failure(
        RestoreExecutionFailureKind failureKind,
        string errorCode,
        RestoreExecutionReport? report = null,
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

public interface IRestoreExecutionService
{
    Task<RestoreExecutionResult> ExecuteAsync(
        RestoreExecutionRequest request,
        CancellationToken cancellationToken = default);
}
