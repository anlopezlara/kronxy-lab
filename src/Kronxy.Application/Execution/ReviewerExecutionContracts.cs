using Kronxy.Application.Artifacts;
using Kronxy.Application.Repositories;

namespace Kronxy.Application.Execution;

public sealed record ReviewerExecutionRequest
{
    public required Guid JobId { get; init; }
    public required Guid RunId { get; init; }
    public required string JobRequest { get; init; }
    public required PlannerPlan Plan { get; init; }
    public required ValidatedDeveloperProposal DeveloperProposal { get; init; }
    public required ObservedChangeManifest ObservedChanges { get; init; }
    public required BuildExecutionReport BuildReport { get; init; }
    public required TestExecutionReport TestReport { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public HumanReviewCorrectionEvidence? HumanReviewCorrection { get; init; }
    public required DeveloperProposalLineage EffectiveProposalLineage { get; init; }
    public required ReviewerEffectiveSourceSnapshot EffectiveSourceSnapshot { get; init; }
    public bool IsSupersedingHumanReviewCorrection { get; init; }
    public int SupersedingReviewVersion { get; init; }
}

public sealed record ReviewerEffectiveSourceFile(
    string RelativePath,
    string Sha256,
    long SizeBytes,
    string Content);

public sealed record ReviewerEffectiveSourceSnapshot(
    Guid JobId,
    Guid RunId,
    DeveloperProposalLineage EffectiveProposalLineage,
    IReadOnlyList<ReviewerEffectiveSourceFile> Files);

public sealed record ReviewerEffectiveSourceSnapshotRequest
{
    public required Guid JobId { get; init; }
    public required Guid RunId { get; init; }
    public required RepositoryWorktreeHandle Repository { get; init; }
    public required PlannerPlan Plan { get; init; }
    public required ValidatedDeveloperProposal EffectiveProposal { get; init; }
    public required ObservedChangeManifest ObservedChanges { get; init; }
    public required DeveloperProposalLineage EffectiveProposalLineage { get; init; }
}

public enum ReviewerEffectiveSourceFailureKind
{
    None = 0,
    InvalidRequest = 10,
    UnsafePath = 20,
    MissingSource = 30,
    StaleSource = 40,
    SourceTooLarge = 50,
    IoFailure = 60,
    Cancelled = 70
}

public sealed record ReviewerEffectiveSourceSnapshotResult(
    ReviewerEffectiveSourceSnapshot? Snapshot,
    ReviewerEffectiveSourceFailureKind FailureKind,
    string ErrorCode)
{
    public bool IsSuccess =>
        FailureKind == ReviewerEffectiveSourceFailureKind.None &&
        Snapshot is not null;

    public static ReviewerEffectiveSourceSnapshotResult Success(
        ReviewerEffectiveSourceSnapshot snapshot) =>
        new(snapshot, ReviewerEffectiveSourceFailureKind.None, string.Empty);

    public static ReviewerEffectiveSourceSnapshotResult Failure(
        ReviewerEffectiveSourceFailureKind kind,
        string errorCode) =>
        new(null, kind, errorCode);
}

public interface IReviewerEffectiveSourceSnapshotService
{
    Task<ReviewerEffectiveSourceSnapshotResult> CaptureAsync(
        ReviewerEffectiveSourceSnapshotRequest request,
        CancellationToken cancellationToken = default);
}

public enum ReviewerExecutionFailureKind
{
    None=0, InvalidRequest=10, AiFailure=20, AiInvalidResponse=30,
    ArtifactWriteFailure=40, Cancelled=50, InternalFailure=60
}

public sealed record ReviewerSupersessionEvidence
{
    public required Guid JobId { get; init; }
    public required Guid RunId { get; init; }
    public required ArtifactType SupersededReviewArtifactType { get; init; }
    public required DeveloperProposalLineage EffectiveProposalLineage { get; init; }
    public required string EffectiveProposalSha256 { get; init; }
    public required string ObservedChangesSha256 { get; init; }
    public required string BuildReportSha256 { get; init; }
    public required string TestReportSha256 { get; init; }
    public int ReviewVersion { get; init; }
    public string EffectiveSourceSnapshotSha256 { get; init; } = string.Empty;
    public string DeterministicAcceptanceGateSha256 { get; init; } = string.Empty;
    public bool ReviewerContradictsDeterministicEvidence { get; init; }
    public required ReviewerDecision Decision { get; init; }
    public required DateTimeOffset RecordedAtUtc { get; init; }
}

public sealed record ReviewerExecutionResult(
    ReviewerReview? Review, ArtifactRecord? ReviewArtifact,
    ArtifactRecord? ResponseArtifact, ReviewerExecutionFailureKind FailureKind, string ErrorCode)
{
    public bool IsSuccess => FailureKind == ReviewerExecutionFailureKind.None &&
        Review is not null && ReviewArtifact is not null && ResponseArtifact is not null;
    public static ReviewerExecutionResult Success(ReviewerReview review, ArtifactRecord reviewArtifact, ArtifactRecord responseArtifact) =>
        new(review, reviewArtifact, responseArtifact, ReviewerExecutionFailureKind.None, string.Empty);
    public static ReviewerExecutionResult Failure(ReviewerExecutionFailureKind kind, string code) =>
        new(null, null, null, kind, code);
}

public interface IReviewerExecutionService
{
    Task<ReviewerExecutionResult> ExecuteAsync(ReviewerExecutionRequest request, CancellationToken cancellationToken = default);
}
