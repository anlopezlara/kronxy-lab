using Kronxy.Application.Artifacts;
using Kronxy.Application.Repositories;

namespace Kronxy.Application.Execution;

public sealed record ObservedChangeManifestEntry(
    string RelativePath,
    ObservedRepositoryChangeKind ChangeKind,
    string? FinalSha256,
    long? FinalSizeBytes,
    DeveloperChangeOperationType? ProposalOperation = null,
    ObservedRepositoryChangeKind? GitChangeKind = null,
    string? BeforeSha256 = null,
    string ValidationResult = "");

public sealed record ObservedChangeManifest(
    Guid JobId,
    Guid RunId,
    string BaseRepositoryHead,
    IReadOnlyList<ObservedChangeManifestEntry> Entries,
    int AttemptCount = 0,
    string ProposalLineageId = "",
    string ProposalFingerprintSha256 = "",
    string SafeChangeReceiptReference = "");

public sealed record ObservedChangeEvidenceRequest
{
    public required Guid JobId { get; init; }
    public required Guid RunId { get; init; }
    public required RepositoryWorktreeHandle Repository { get; init; }
    public required ValidatedDeveloperProposal Proposal { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public int AttemptCount { get; init; }
    public string ProposalLineageId { get; init; } = string.Empty;
    public string ProposalFingerprintSha256 { get; init; } = string.Empty;
    public string SafeChangeReceiptReference { get; init; } = string.Empty;
    public IReadOnlyList<AppliedFileChange> SafeChangeChanges { get; init; } = [];

    public bool IsBuildCorrection { get; init; }
    public bool IsBuildCorrectionRetry { get; init; }
    public bool IsHumanReviewCorrection { get; init; }
    public bool IsGovernedHumanCorrection { get; init; }
    public IReadOnlyList<string> AllowedPaths { get; init; } = [];
}

public enum ObservedChangeEvidenceFailureKind
{
    None = 0,
    InvalidRequest = 10,
    RepositoryFailure = 20,
    EvidenceMismatch = 30,
    UnsafePath = 40,
    ArtifactWriteFailure = 50,
    IoFailure = 60,
    Cancelled = 70
}

public sealed record ObservedChangeEvidenceResult(
    ObservedChangeManifest? Manifest,
    ArtifactRecord? ManifestArtifact,
    ObservedChangeEvidenceFailureKind FailureKind,
    string ErrorCode)
{
    public bool IsSuccess => FailureKind == ObservedChangeEvidenceFailureKind.None &&
        Manifest is not null && ManifestArtifact is not null;

    public static ObservedChangeEvidenceResult Success(
        ObservedChangeManifest manifest,
        ArtifactRecord artifact) =>
        new(manifest, artifact, ObservedChangeEvidenceFailureKind.None, string.Empty);

    public static ObservedChangeEvidenceResult Failure(
        ObservedChangeEvidenceFailureKind kind,
        string errorCode) =>
        new(null, null, kind, errorCode);
}

public interface IObservedChangeEvidenceService
{
    Task<ObservedChangeEvidenceResult> CaptureAsync(
        ObservedChangeEvidenceRequest request,
        CancellationToken cancellationToken = default);
}
