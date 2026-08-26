using Kronxy.Context.Configuration;
using Kronxy.Context.Files;
using Kronxy.Context.Git;
using Xunit;

namespace Kronxy.Context.Tests.Files;

public sealed class RepositoryFileInventoryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "kronxy-inventory-tests", Guid.NewGuid().ToString("N"));

    public RepositoryFileInventoryTests() => Directory.CreateDirectory(root);

    [Fact]
    public async Task CombinesSortsAndClassifiesTrackedAndUntrackedWithoutReadingContent()
    {
        var git = new InventoryGitClient(root,
            [Entry("z.cs"), Entry("link", GitIndexEntryType.SymbolicLink)],
            ["a.md"]);
        var fs = new MetadataOnlyFileSystem(10);
        var result = await Inventory(git, fs).CreateAsync(root, Limits(), OutputDirectory);

        Assert.True(result.IsComplete);
        Assert.Equal(["a.md", "z.cs"], result.Accepted.Select(file => file.LogicalPath));
        Assert.Contains(result.Excluded, file => file.LogicalPath == "link" && file.Reason == FileExclusionReason.SymbolicLink);
        Assert.Equal(0, fs.OpenCount);
    }

    [Fact]
    public async Task DuplicateConflictMissingAndOversizeAreExplicit()
    {
        var git = new InventoryGitClient(root,
            [Entry("dup.cs"), Entry("dup.cs"), Entry("conflict.cs", stage: 2), Entry("missing.cs"), Entry("large.cs")], []);
        var fs = new MetadataOnlyFileSystem(5) { MissingName = "missing.cs", LargeName = "large.cs" };
        var result = await Inventory(git, fs).CreateAsync(root, Limits(maxIndividual: 10), OutputDirectory);

        Assert.Contains(result.Excluded, item => item.Reason == FileExclusionReason.Duplicate);
        Assert.Contains(result.Excluded, item => item.Reason == FileExclusionReason.Conflict);
        Assert.Contains(result.Excluded, item => item.Reason == FileExclusionReason.Missing);
        Assert.Contains(result.Excluded, item => item.Reason == FileExclusionReason.TooLarge);
    }

    [Fact]
    public async Task CandidateAcceptedAndTotalLimitsFailWithoutPartialInventory()
    {
        var git = new InventoryGitClient(root, [Entry("a"), Entry("b")], []);
        var fs = new MetadataOnlyFileSystem(6);
        var candidate = await Inventory(git, fs).CreateAsync(root, Limits(maxCandidates: 1, maxAccepted: 1), OutputDirectory);
        var accepted = await Inventory(git, fs).CreateAsync(root, Limits(maxAccepted: 1), OutputDirectory);
        var total = await Inventory(git, fs).CreateAsync(root, Limits(maxTotal: 10), OutputDirectory);

        Assert.Equal(RepositoryInventoryStatus.CandidateLimitExceeded, candidate.Status);
        Assert.Equal(RepositoryInventoryStatus.AcceptedFileLimitExceeded, accepted.Status);
        Assert.Equal(RepositoryInventoryStatus.TotalSizeLimitExceeded, total.Status);
        Assert.Empty(candidate.Accepted);
        Assert.Empty(accepted.Accepted);
        Assert.Empty(total.Accepted);
    }

    [Fact]
    public async Task CaseCollisionFollowsPlatformRules()
    {
        var git = new InventoryGitClient(root, [Entry("File.cs"), Entry("file.cs")], []);
        var result = await Inventory(git, new MetadataOnlyFileSystem(1)).CreateAsync(root, Limits(), OutputDirectory);
        Assert.Equal(OperatingSystem.IsWindows(), result.Excluded.Any(item => item.Reason == FileExclusionReason.CaseCollision));
    }

    [Fact]
    public async Task ExtremeMetadataLengthIsExcludedWithoutArithmeticOverflow()
    {
        var git = new InventoryGitClient(root, [Entry("huge.txt")], []);
        var result = await Inventory(git, new MetadataOnlyFileSystem(long.MaxValue))
            .CreateAsync(root, Limits(), OutputDirectory);
        Assert.True(result.IsComplete);
        Assert.Equal(FileExclusionReason.TooLarge, Assert.Single(result.Excluded).Reason);
    }

    [Fact]
    public async Task SameServiceAcceptsDifferentExplicitTotalBudgets()
    {
        var git = new InventoryGitClient(root, [Entry("a"), Entry("b")], []);
        var service = Inventory(git, new MetadataOnlyFileSystem(6));

        var small = await service.CreateAsync(root, Limits(maxTotal: 10), OutputDirectory);
        var large = await service.CreateAsync(root, Limits(maxTotal: 12), OutputDirectory);

        Assert.Equal(RepositoryInventoryStatus.TotalSizeLimitExceeded, small.Status);
        Assert.True(large.IsComplete);
        Assert.Equal(2, large.Accepted.Count);
    }

    [Fact]
    public async Task MultipleConflictStagesRemainConflictsRatherThanDuplicates()
    {
        var git = new InventoryGitClient(root,
            [Entry("conflict.cs", stage: 1), Entry("conflict.cs", stage: 2), Entry("conflict.cs", stage: 3)], []);
        var result = await Inventory(git, new MetadataOnlyFileSystem(1))
            .CreateAsync(root, Limits(), OutputDirectory);

        Assert.Equal(3, result.Excluded.Count(item => item.Reason == FileExclusionReason.Conflict));
        Assert.DoesNotContain(result.Excluded, item => item.Reason == FileExclusionReason.Duplicate);
    }

    private RepositoryFileInventory Inventory(IGitClient git, IFileSystemAccess fs) =>
        new(git, new FileSelectionPolicy(), new RepositoryPathResolver(), fs);

    private const string OutputDirectory = ".kronxy-context/packages";

    private static RepositoryInventoryLimits Limits(
        int maxCandidates = 100,
        int maxAccepted = 100,
        int maxIndividual = 1_000,
        long maxTotal = 10_000) => new()
        {
            MaxCandidates = maxCandidates,
            MaxAcceptedFiles = maxAccepted,
            MaxIndividualFileBytes = maxIndividual,
            MaxTotalBytes = maxTotal,
            MaxLogicalPathLength = 1_024
        };

    private static GitIndexEntry Entry(string path, GitIndexEntryType type = GitIndexEntryType.RegularFile, int stage = 0) => new()
    { Path = path, Mode = "100644", ObjectId = new string('a', 40), Stage = stage, EntryType = type };

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
        var parent = Path.GetDirectoryName(root)!;
        if (Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent);
    }

    private sealed class MetadataOnlyFileSystem(long size) : IFileSystemAccess
    {
        public string? MissingName { get; init; }
        public string? LargeName { get; init; }
        public int OpenCount { get; private set; }
        public FileMetadata GetMetadata(string path)
        {
            if (path.EndsWith(MissingName ?? "\0", StringComparison.Ordinal)) return new(false, 0, default, default);
            var length = path.EndsWith(LargeName ?? "\0", StringComparison.Ordinal) ? 100 : size;
            return new(true, length, DateTime.UnixEpoch, FileAttributes.Normal);
        }
        public Stream OpenRead(string path) { OpenCount++; return Stream.Null; }
    }

    private sealed class InventoryGitClient(
        string root,
        IReadOnlyList<GitIndexEntry> index,
        IReadOnlyList<string> untracked) : IGitClient
    {
        public Task<string> DiscoverRootAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(root);
        public Task<IReadOnlyList<GitIndexEntry>> GetIndexEntriesAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(index);
        public Task<IReadOnlyList<string>> GetUntrackedFilesAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(untracked);
        public Task<RepositoryInfo> GetRepositoryInfoAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> ResolveCommitAsync(string path, string reference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkingTreeStatus> GetWorkingTreeStatusAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<GitChange>> GetChangesAsync(string path, string fromReference, string toReference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsAncestorAsync(string path, string possibleAncestorReference, string descendantReference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
