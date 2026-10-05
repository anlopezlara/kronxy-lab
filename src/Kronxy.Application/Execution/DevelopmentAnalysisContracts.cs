using Kronxy.Application.Artifacts;
using Kronxy.Application.Repositories;

namespace Kronxy.Application.Execution;

public enum DevelopmentChangeClassification
{
    NewComponent,
    Extension,
    LocalRefactor,
    CrossLayerChange,
    BreakingChange,
    AlreadySatisfied,
    ArchitectureConflict,
    Unknown
}

public sealed record DevelopmentAnalysisEvidence(
    string Kind,
    string Path,
    string Detail);

public sealed record DevelopmentAnalysis
{
    public required Guid JobId { get; init; }
    public required Guid RunId { get; init; }
    public required int AttemptCount { get; init; }
    public required string RequestIdentity { get; init; }
    public required IReadOnlyList<string> TargetSymbols { get; init; }
    public required IReadOnlyList<string> ExistingDeclarations { get; init; }
    public required DevelopmentChangeClassification PrimaryClassification { get; init; }
    public required IReadOnlyList<string> ImpactedLayers { get; init; }
    public required string RequestedScope { get; init; }
    public required string RequiredScope { get; init; }
    public required bool? ScopeCompatible { get; init; }
    public required IReadOnlyList<string> BreakingContracts { get; init; }
    public required bool ArchitectureDecisionRequired { get; init; }
    public required bool DeveloperExecutionAllowed { get; init; }
    public required IReadOnlyList<DevelopmentAnalysisEvidence> Evidence { get; init; }
    public required IReadOnlyList<string> FilesInspected { get; init; }
    public required string AnalysisVersion { get; init; }
}

public sealed record DevelopmentAnalysisRequest
{
    public required Guid JobId { get; init; }
    public required Guid RunId { get; init; }
    public required int AttemptCount { get; init; }
    public required string JobRequest { get; init; }
    public required RepositoryWorktreeHandle Repository { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
}

public sealed record DevelopmentAnalysisResult(
    DevelopmentAnalysis? Analysis,
    ArtifactRecord? Artifact,
    string ErrorCode)
{
    public bool IsSuccess => Analysis is not null && Artifact is not null && string.IsNullOrEmpty(ErrorCode);
    public static DevelopmentAnalysisResult Success(DevelopmentAnalysis analysis, ArtifactRecord artifact) => new(analysis, artifact, string.Empty);
    public static DevelopmentAnalysisResult Failure(string errorCode) => new(null, null, errorCode);
}

public interface IDevelopmentAnalysisService
{
    Task<DevelopmentAnalysisResult> AnalyzeAsync(
        DevelopmentAnalysisRequest request,
        CancellationToken cancellationToken = default);
}
