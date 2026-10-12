using Kronxy.Application.Repositories;

namespace Kronxy.Application.Execution;

public enum SourcePromotionStatus
{
    Prepared = 10,
    Promoted = 20
}

public sealed record SourcePromotionEvidence
{
    public int SchemaVersion { get; init; } = 1;
    public required Guid JobId { get; init; }
    public required string ExternalId { get; init; }
    public required Guid RunId { get; init; }
    public required int AttemptCount { get; init; }
    public required string CorrelationId { get; init; }
    public required string BaseRepositoryHead { get; init; }
    public required string ApprovedSourceSnapshotSha256 { get; init; }
    public required string ObservedChangesSha256 { get; init; }
    public required string BuildReportSha256 { get; init; }
    public required string TestReportSha256 { get; init; }
    public required string ReviewerReviewSha256 { get; init; }
    public required string HumanReviewApprovalEvidenceSha256 { get; init; }
    public required string CommitSha { get; init; }
    public required string CommitTreeSha { get; init; }
    public required string JobRef { get; init; }
    public required string TargetRef { get; init; }
    public required string ExpectedTargetHead { get; init; }
    public required string PreviousTargetHead { get; init; }
    public required string ResultingTargetHead { get; init; }
    public required string PromotionMode { get; init; }
    public required SourcePromotionStatus Status { get; init; }
    public required string Actor { get; init; }
    public required DateTimeOffset RecordedAtUtc { get; init; }
    public required string IdempotencyKey { get; init; }
    public required IReadOnlyList<string> GovernedPaths { get; init; }
}
