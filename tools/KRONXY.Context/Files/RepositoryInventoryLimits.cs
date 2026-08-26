using Kronxy.Context.Configuration;

namespace Kronxy.Context.Files;

public sealed record RepositoryInventoryLimits
{
    public required int MaxCandidates { get; init; }
    public required int MaxAcceptedFiles { get; init; }
    public required int MaxIndividualFileBytes { get; init; }
    public required long MaxTotalBytes { get; init; }
    public required int MaxLogicalPathLength { get; init; }

    public static RepositoryInventoryLimits FromOptions(ContextOptions options, long maxTotalBytes)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new RepositoryInventoryLimits
        {
            MaxCandidates = options.MaxRepositoryCandidates,
            MaxAcceptedFiles = options.MaxFilesPerPackage,
            MaxIndividualFileBytes = options.MaxTextFileBytes,
            MaxTotalBytes = maxTotalBytes,
            MaxLogicalPathLength = options.MaxLogicalPathLength
        };
    }

    internal void Validate()
    {
        if (MaxCandidates <= 0 || MaxAcceptedFiles <= 0 || MaxAcceptedFiles > MaxCandidates ||
            MaxIndividualFileBytes <= 0 || MaxTotalBytes <= 0 || MaxLogicalPathLength <= 0)
        {
            throw new ArgumentException("Los límites del inventario no son válidos.");
        }
    }
}
