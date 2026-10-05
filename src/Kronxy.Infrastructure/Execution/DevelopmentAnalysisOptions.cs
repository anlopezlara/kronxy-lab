namespace Kronxy.Infrastructure.Execution;

public sealed record DevelopmentAnalysisOptions
{
    public int MaxFilesInspected { get; init; } = 1000;
    public int MaxReferenceMatches { get; init; } = 200;
    public int MaxCandidateTargets { get; init; } = 8;
    public int MaxEvidenceItems { get; init; } = 300;

    public void Validate()
    {
        if (MaxFilesInspected is < 1 or > 5000 || MaxReferenceMatches is < 1 or > 1000 ||
            MaxCandidateTargets is < 1 or > 32 || MaxEvidenceItems is < 1 or > 2000)
            throw new InvalidOperationException("DevelopmentAnalysis options are invalid.");
    }
}
