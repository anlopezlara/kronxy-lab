using Kronxy.Context.Git;

namespace Kronxy.Context.Files;

public interface IRepositoryFileInventory
{
    Task<RepositoryFileInventoryResult> CreateAsync(
        string repositoryRoot,
        RepositoryInventoryLimits limits,
        string outputDirectory,
        CancellationToken cancellationToken = default);
}

public sealed class RepositoryFileInventory : IRepositoryFileInventory
{
    private readonly IGitClient gitClient;
    private readonly IFileSelectionPolicy policy;
    private readonly IRepositoryPathResolver resolver;
    private readonly IFileSystemAccess fileSystem;

    public RepositoryFileInventory(IGitClient gitClient)
        : this(gitClient, new FileSelectionPolicy(), new RepositoryPathResolver(), new FileSystemAccess()) { }

    internal RepositoryFileInventory(
        IGitClient gitClient,
        IFileSelectionPolicy policy,
        IRepositoryPathResolver resolver,
        IFileSystemAccess fileSystem)
    {
        this.gitClient = gitClient ?? throw new ArgumentNullException(nameof(gitClient));
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
        this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public async Task<RepositoryFileInventoryResult> CreateAsync(
        string repositoryRoot,
        RepositoryInventoryLimits limits,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();

        var root = await gitClient.DiscoverRootAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
        var index = await gitClient.GetIndexEntriesAsync(root, cancellationToken).ConfigureAwait(false);
        var untracked = await gitClient.GetUntrackedFilesAsync(root, cancellationToken).ConfigureAwait(false);
        var candidateCount = checked((long)index.Count + untracked.Count);
        if (candidateCount > limits.MaxCandidates)
            return Failure(RepositoryInventoryStatus.CandidateLimitExceeded);

        var candidates = index.Select(entry => new RepositoryFileCandidate
        {
            LogicalPath = entry.Path,
            Origin = RepositoryFileOrigin.Tracked,
            GitEntryType = entry.EntryType,
            Stage = entry.Stage
        }).Concat(untracked.Select(path => new RepositoryFileCandidate
        {
            LogicalPath = path,
            Origin = RepositoryFileOrigin.Untracked
        })).OrderBy(candidate => Normalize(candidate.LogicalPath), StringComparer.Ordinal).ToArray();

        var accepted = new List<RepositoryInventoryFile>();
        var excluded = new List<RepositoryExcludedFile>();
        var exactPaths = new HashSet<string>(StringComparer.Ordinal);
        var platformPaths = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        long total = 0;

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var logical = Normalize(candidate.LogicalPath);
            if (candidate.IsConflict) { Exclude(logical, FileExclusionReason.Conflict); continue; }
            if (!exactPaths.Add(logical)) { Exclude(logical, FileExclusionReason.Duplicate); continue; }
            if (!platformPaths.Add(logical)) { Exclude(logical, FileExclusionReason.CaseCollision); continue; }

            var decision = policy.Evaluate(candidate with { LogicalPath = logical }, limits, outputDirectory);
            if (!decision.Include) { Exclude(logical, decision.Reason); continue; }

            var resolution = resolver.Resolve(root, logical);
            if (!resolution.Success)
            {
                Exclude(logical, resolution.Error == PathResolutionError.OutsideRepository
                    ? FileExclusionReason.OutsideRepository : FileExclusionReason.InvalidPath);
                continue;
            }

            var physicalReason = RepositoryPhysicalPathSecurity.Inspect(root, resolution.PhysicalPath!, fileSystem);
            if (physicalReason != FileExclusionReason.None) { Exclude(logical, physicalReason); continue; }

            FileMetadata metadata;
            try { metadata = fileSystem.GetMetadata(resolution.PhysicalPath!); }
            catch (UnauthorizedAccessException) { Exclude(logical, FileExclusionReason.AccessDenied); continue; }
            catch (IOException) { Exclude(logical, FileExclusionReason.IoError); continue; }
            if (metadata.Length > limits.MaxIndividualFileBytes) { Exclude(logical, FileExclusionReason.TooLarge); continue; }
            if (accepted.Count >= limits.MaxAcceptedFiles)
                return Failure(RepositoryInventoryStatus.AcceptedFileLimitExceeded, excluded);
            if (metadata.Length > limits.MaxTotalBytes - total)
                return Failure(RepositoryInventoryStatus.TotalSizeLimitExceeded, excluded);

            total += metadata.Length;
            accepted.Add(new RepositoryInventoryFile
            {
                LogicalPath = logical,
                Origin = candidate.Origin,
                SizeBytes = metadata.Length,
                IsExecutable = candidate.IsExecutable
            });
        }

        return new RepositoryFileInventoryResult { Accepted = accepted, Excluded = excluded };

        void Exclude(string path, FileExclusionReason reason) =>
            excluded.Add(new RepositoryExcludedFile { LogicalPath = path, Reason = reason });
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static RepositoryFileInventoryResult Failure(
        RepositoryInventoryStatus status,
        IReadOnlyList<RepositoryExcludedFile>? excluded = null) => new()
        {
            Status = status,
            Accepted = [],
            Excluded = excluded ?? []
        };
}
