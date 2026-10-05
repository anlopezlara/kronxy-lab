namespace Kronxy.Application.Execution;

public enum ArchitectureDecisionKind
{
    PreserveExistingArchitecture,
    AuthorizeScopeExpansion,
    SupersedeRequest,
    NoCodeChangeRequired
}

public sealed record ArchitectureDecisionRequest
{
    public required string Actor { get; init; }
    public required string CorrelationId { get; init; }
    public required ArchitectureDecisionKind Decision { get; init; }
    public required string Reason { get; init; }
    public required bool FollowUpRequired { get; init; }
    public string? FollowUpDescription { get; init; }
}

public sealed record ArchitectureDecisionEvidence
{
    public required Guid JobId { get; init; }
    public required Guid RunId { get; init; }
    public required int AttemptCount { get; init; }
    public required string Actor { get; init; }
    public required string CorrelationId { get; init; }
    public required ArchitectureDecisionKind Decision { get; init; }
    public required string Reason { get; init; }
    public required string DevelopmentAnalysisArtifactReference { get; init; }
    public required string DevelopmentAnalysisSha256 { get; init; }
    public required DevelopmentChangeClassification Classification { get; init; }
    public required string RequestedScope { get; init; }
    public required string RequiredScope { get; init; }
    public required IReadOnlyList<string> ImpactedLayers { get; init; }
    public required bool SourceMutation { get; init; }
    public required bool FollowUpRequired { get; init; }
    public string? FollowUpDescription { get; init; }
    public required DateTimeOffset RecordedAtUtc { get; init; }
}
