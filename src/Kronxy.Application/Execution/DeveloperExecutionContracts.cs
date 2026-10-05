using Kronxy.Application.Artifacts;
using Kronxy.Application.Repositories;

namespace Kronxy.Application.Execution;

public enum DeveloperExecutionFailureKind
{
    None = 0,
    InvalidRequest = 10,
    PlanningArtifactReadFailure = 20,
    PlanningPlanInvalid = 30,
    ContextArtifactReadFailure = 40,
    ContextPackageInvalid = 50,
    ContextTooLarge = 60,
    AiRejected = 70,
    AiTimedOut = 80,
    AiCancelled = 90,
    AiUnavailable = 100,
    AiProviderError = 110,
    AiInvalidResponse = 120,
    PolicyRejected = 130,
    ArtifactWriteFailure = 140,
    Cancelled = 150,
    InternalFailure = 160
}

public sealed record DeveloperExecutionRequest
{
    public required Guid JobId { get; init; }
    public required Guid RunId { get; init; }
    public required string JobRequest { get; init; }
    public required string CorrelationId { get; init; }
    public required RepositoryWorktreeHandle Repository { get; init; }
    public Guid? AuthorizedEvidenceRunId { get; init; }
    public ReviewerReview? ReviewerFeedback { get; init; }
    public DeveloperBuildCorrectionContext? BuildCorrection { get; init; }
    public DeveloperHumanReviewCorrectionContext? HumanReviewCorrection { get; init; }
}

public sealed record DeveloperProposalMetadataBindingRequest
{
    public required Guid JobId { get; init; }
    public required RepositoryWorktreeHandle Repository { get; init; }
    public required DeveloperProposal Proposal { get; init; }
    public required IReadOnlyList<string> AllowedPaths { get; init; }
}

public sealed record DeveloperProposalMetadataBindingResult(
    DeveloperProposal? Proposal,
    string ErrorCode)
{
    public bool IsSuccess =>
        Proposal is not null &&
        string.IsNullOrEmpty(ErrorCode);

    public static DeveloperProposalMetadataBindingResult Success(
        DeveloperProposal proposal) =>
        new(proposal, string.Empty);

    public static DeveloperProposalMetadataBindingResult Failure(
        string errorCode) =>
        new(null, errorCode);
}

public interface IDeveloperProposalMetadataBinder
{
    Task<DeveloperProposalMetadataBindingResult> BindAsync(
        DeveloperProposalMetadataBindingRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record DeveloperHumanReviewCorrectionContext
{
    public required ValidatedDeveloperProposal CurrentProposal { get; init; }
    public required BuildExecutionReport BuildReport { get; init; }
    public required TestExecutionReport TestReport { get; init; }
    public required ReviewerReview ReviewerReview { get; init; }
    public required HumanReviewCorrectionEvidence HumanReviewEvidence { get; init; }
}

public sealed record DeveloperBuildCorrectionContext
{
    public required ValidatedDeveloperProposal OriginalProposal { get; init; }
    public required BuildExecutionReport FailedBuildReport { get; init; }
    public required string BuildStandardOutput { get; init; }
    public IReadOnlyList<string> PreviousNoOpPaths { get; init; } = [];
}

public sealed record DeveloperExecutionReport
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

public sealed record DeveloperExecutionResult(
    DeveloperExecutionReport? Report,
    ArtifactRecord? DeveloperResponseArtifact,
    ArtifactRecord? DeveloperProposalArtifact,
    ValidatedDeveloperProposal? Proposal,
    DeveloperExecutionFailureKind FailureKind,
    string ErrorCode)
{
    public bool IsSuccess =>
        FailureKind == DeveloperExecutionFailureKind.None &&
        Report is not null &&
        DeveloperResponseArtifact is not null &&
        DeveloperProposalArtifact is not null &&
        Proposal is not null;

    public static DeveloperExecutionResult Success(
        DeveloperExecutionReport report,
        ArtifactRecord developerResponseArtifact,
        ArtifactRecord developerProposalArtifact,
        ValidatedDeveloperProposal proposal) =>
        new(report, developerResponseArtifact,
            developerProposalArtifact, proposal,
            DeveloperExecutionFailureKind.None, string.Empty);

    public static DeveloperExecutionResult Failure(
        DeveloperExecutionFailureKind kind,
        string errorCode) =>
        new(null, null, null, null, kind, errorCode);
}

public interface IDeveloperExecutionService
{
    Task<DeveloperExecutionResult> ExecuteAsync(
        DeveloperExecutionRequest request,
        CancellationToken cancellationToken = default);
}
