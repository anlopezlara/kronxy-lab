namespace Kronxy.Application.Artifacts;

public enum ArtifactType
{
    ContextPackage = 10,
    AiResponse = 20,
    PlanningPlan = 21,
    DeveloperResponse = 22,
    DeveloperProposal = 23,
    ObservedChangeManifest = 24,
    ReviewerResponse = 25,
    ReviewerReview = 26,
    PlanningRejectedResponse = 27,
    DeveloperRejectedResponse = 28,
    DeveloperRejectedStructuredResponse = 29,

    DeveloperBuildCorrectionResponse = 70,
    DeveloperBuildCorrectionProposal = 71,
    ObservedBuildCorrectionManifest = 72,
    BuildCorrectionReport = 73,
    BuildCorrectionStandardOutput = 74,
    BuildCorrectionStandardError = 75,
    DeveloperBuildCorrectionRejectedResponse = 76,
    DeveloperBuildCorrectionRejectedStructuredResponse = 77,
    DeveloperBuildCorrectionRetryResponse = 78,
    DeveloperBuildCorrectionRetryProposal = 79,
    DeveloperBuildCorrectionRetryRejectedResponse = 105,
    DeveloperBuildCorrectionRetryRejectedStructuredResponse = 106,
    ObservedBuildCorrectionRetryManifest = 107,
    BuildCorrectionRetryReport = 108,
    BuildCorrectionRetryStandardOutput = 109,
    BuildCorrectionRetryStandardError = 110,

    HumanReviewCorrectionEvidence = 80,
    DeveloperHumanReviewCorrectionResponse = 81,
    DeveloperHumanReviewCorrectionProposal = 82,
    DeveloperHumanReviewCorrectionRejectedResponse = 83,
    DeveloperHumanReviewCorrectionRejectedStructuredResponse = 84,
    ObservedHumanReviewCorrectionManifest = 85,
    BuildHumanReviewCorrectionReport = 86,
    BuildHumanReviewCorrectionStandardOutput = 87,
    BuildHumanReviewCorrectionStandardError = 88,
    TestHumanReviewCorrectionReport = 89,
    TestHumanReviewCorrectionResults = 90,
    TestHumanReviewCorrectionStandardOutput = 91,
    TestHumanReviewCorrectionStandardError = 92,
    ReviewerHumanReviewCorrectionResponse = 93,
    ReviewerHumanReviewCorrectionReview = 94,
    ReviewerHumanReviewCorrectionSupersedingResponse = 95,
    ReviewerHumanReviewCorrectionSupersedingReview = 96,
    ReviewerHumanReviewCorrectionSupersessionEvidence = 97,
    ReviewerHumanReviewCorrectionSourceAwareSupersedingResponse = 98,
    ReviewerHumanReviewCorrectionSourceAwareSupersedingReview = 99,
    ReviewerHumanReviewCorrectionSourceAwareSupersessionEvidence = 100,
    ReviewerHumanReviewCorrectionDeterministicSupersedingResponse = 101,
    ReviewerHumanReviewCorrectionDeterministicSupersedingReview = 102,
    ReviewerHumanReviewCorrectionDeterministicSupersessionEvidence = 103,
    HumanReviewApprovalEvidence = 104,
    GovernedHumanCorrectionRequest = 111,
    GovernedHumanCorrectionReceipt = 112,
    ObservedGovernedHumanCorrectionManifest = 113,
    DevelopmentAnalysis = 114,

    RestoreReport = 30,
    RestoreStandardOutput = 31,
    RestoreStandardError = 32,

    BuildReport = 40,
    BuildStandardOutput = 41,
    BuildStandardError = 42,

    TestReport = 50,
    TestResults = 51,
    TestStandardOutput = 52,
    TestStandardError = 53,

    GeneralReport = 60
}

public static class ArtifactTypeClassification
{
    public static bool IsVersionedDeveloperCorrection(
        this ArtifactType artifactType) =>
        artifactType is
            ArtifactType.DeveloperBuildCorrectionResponse or
            ArtifactType.DeveloperBuildCorrectionProposal or
            ArtifactType.DeveloperBuildCorrectionRejectedResponse or
            ArtifactType.DeveloperBuildCorrectionRejectedStructuredResponse or
            ArtifactType.DeveloperBuildCorrectionRetryResponse or
            ArtifactType.DeveloperBuildCorrectionRetryProposal or
            ArtifactType.DeveloperBuildCorrectionRetryRejectedResponse or
            ArtifactType.DeveloperBuildCorrectionRetryRejectedStructuredResponse or
            ArtifactType.DeveloperHumanReviewCorrectionResponse or
            ArtifactType.DeveloperHumanReviewCorrectionProposal or
            ArtifactType.DeveloperHumanReviewCorrectionRejectedResponse or
            ArtifactType.DeveloperHumanReviewCorrectionRejectedStructuredResponse;

    public static bool IsVersionedHumanCorrection(
        this ArtifactType artifactType) =>
        artifactType is
            ArtifactType.GovernedHumanCorrectionRequest or
            ArtifactType.GovernedHumanCorrectionReceipt or
            ArtifactType.ObservedGovernedHumanCorrectionManifest;
}

public enum ArtifactStoreFailureKind
{
    None = 0,
    InvalidRequest = 10,
    UnsafeRoot = 20,
    UnsafePath = 30,
    ArtifactTooLarge = 40,
    DestinationExists = 50,
    IntegrityFailure = 60,
    IoFailure = 70,
    Cancelled = 80
}

public sealed record ArtifactWriteRequest
{
    public required Guid JobId { get; init; }

    public required Guid RunId { get; init; }

    public required ArtifactType ArtifactType { get; init; }

    public required ReadOnlyMemory<byte> Content { get; init; }

    public string CorrelationId { get; init; } = string.Empty;
}

public sealed record ArtifactRecord
{
    public required Guid ArtifactId { get; init; }

    public required Guid JobId { get; init; }

    public required Guid RunId { get; init; }

    public required ArtifactType ArtifactType { get; init; }

    public required string RelativePath { get; init; }

    public required string Sha256 { get; init; }

    public required long SizeBytes { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required string CorrelationId { get; init; }
}

public sealed record ArtifactWriteResult(
    ArtifactRecord? Artifact,
    ArtifactStoreFailureKind FailureKind,
    string ErrorCode)
{
    public bool IsSuccess =>
        FailureKind == ArtifactStoreFailureKind.None &&
        Artifact is not null;

    public static ArtifactWriteResult Success(
        ArtifactRecord artifact) =>
        new(
            artifact,
            ArtifactStoreFailureKind.None,
            string.Empty);

    public static ArtifactWriteResult Failure(
        ArtifactStoreFailureKind kind,
        string errorCode) =>
        new(
            null,
            kind,
            errorCode);
}

public interface IArtifactStore
{
    Task<ArtifactWriteResult> WriteAsync(
        ArtifactWriteRequest request,
        CancellationToken cancellationToken = default);
}

public interface IArtifactMetadataRepository
{
    Task AddAsync(
        ArtifactRecord artifact,
        CancellationToken cancellationToken = default);

    Task<ArtifactRecord?> GetByIdAsync(
        Guid artifactId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ArtifactRecord>> GetByJobAndRunAsync(
        Guid jobId,
        Guid runId,
        CancellationToken cancellationToken = default);
}
