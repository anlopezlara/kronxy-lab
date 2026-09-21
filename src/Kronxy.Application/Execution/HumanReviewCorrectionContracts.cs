namespace Kronxy.Application.Execution;

public enum HumanReviewDecision
{
    ChangesRequired = 10,
    Approved = 20
}

public sealed record HumanReviewApprovalEvidence
{
    public required Guid JobId { get; init; }
    public required Guid RunId { get; init; }
    public required int AttemptCount { get; init; }
    public required HumanReviewDecision Decision { get; init; }
    public required string Actor { get; init; }
    public required string CorrelationId { get; init; }
    public required string ReviewerReviewSha256 { get; init; }
    public required string DeterministicAcceptanceGateSha256 { get; init; }
    public required DateTimeOffset RecordedAtUtc { get; init; }
}

public sealed record HumanReviewRequiredCorrection
{
    public required string RelativePath { get; init; }
    public required string Instruction { get; init; }
}

public sealed record HumanReviewCorrectionEvidence
{
    public required Guid JobId { get; init; }
    public required Guid RunId { get; init; }
    public required HumanReviewDecision Decision { get; init; }
    public required IReadOnlyList<HumanReviewRequiredCorrection> RequiredCorrections { get; init; }
    public required DateTimeOffset RecordedAtUtc { get; init; }
}

public sealed record HumanReviewCorrectionRequest
{
    public required IReadOnlyList<HumanReviewRequiredCorrection> RequiredCorrections { get; init; }
    public string Actor { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
}
