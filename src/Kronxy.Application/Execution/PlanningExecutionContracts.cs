using Kronxy.Application.Artifacts;

namespace Kronxy.Application.Execution;

public enum PlanningExecutionFailureKind
{
    None = 0,
    InvalidRequest = 10,
    ContextArtifactReadFailure = 20,
    ContextPackageInvalid = 30,
    ContextTooLarge = 40,
    AiRejected = 50,
    AiTimedOut = 60,
    AiCancelled = 70,
    AiUnavailable = 80,
    AiProviderError = 90,
    AiInvalidResponse = 100,
    ArtifactWriteFailure = 110,
    Cancelled = 120,
    InternalFailure = 130
}

public sealed record PlanningExecutionRequest
{
    public required Guid JobId { get; init; }

    public required Guid RunId { get; init; }

    public required string JobRequest { get; init; }

    public required string CorrelationId { get; init; }
}

public sealed record PlanningExecutionReport
{
    public required Guid JobId { get; init; }

    public required Guid RunId { get; init; }

    public required string LogicalModel { get; init; }

    public required string Provider { get; init; }

    public required string PhysicalModel { get; init; }

    public required TimeSpan Duration { get; init; }

    public required string TerminationReason { get; init; }

    public long? PromptTokens { get; init; }

    public long? CompletionTokens { get; init; }
}

public sealed record PlanningExecutionResult(
    PlanningExecutionReport? Report,
    ArtifactRecord? AiResponseArtifact,
    PlanningExecutionFailureKind FailureKind,
    string ErrorCode)
{
    public bool IsSuccess =>
        FailureKind ==
            PlanningExecutionFailureKind.None &&
        Report is not null &&
        AiResponseArtifact is not null;

    public static PlanningExecutionResult Success(
        PlanningExecutionReport report,
        ArtifactRecord artifact) =>
        new(
            report,
            artifact,
            PlanningExecutionFailureKind.None,
            string.Empty);

    public static PlanningExecutionResult Failure(
        PlanningExecutionFailureKind kind,
        string errorCode) =>
        new(
            null,
            null,
            kind,
            errorCode);
}

public interface IPlanningExecutionService
{
    Task<PlanningExecutionResult> ExecuteAsync(
        PlanningExecutionRequest request,
        CancellationToken cancellationToken = default);
}
