using Kronxy.Context.Git;
using Kronxy.Context.Processes;
using Xunit;

namespace Kronxy.Context.Tests.Git;

public sealed class GitClientIntegrationTests
{
    private readonly GitClient client = new(new ProcessRunner());

    [Fact]
    public async Task CleanRepositoryAndRepositoryInfoAreDetected()
    {
        using var repository = InitializedRepository();

        var info = await client.GetRepositoryInfoAsync(repository.RootPath);
        var status = await client.GetWorkingTreeStatusAsync(repository.RootPath);

        Assert.Equal(Path.GetFullPath(repository.RootPath), info.RootPath);
        Assert.Equal("main", info.Branch);
        Assert.False(info.IsHeadDetached);
        Assert.Matches("^[0-9a-f]{40,64}$", info.HeadCommit);
        Assert.True(status.IsClean);
    }

    [Fact]
    public async Task WorkingTreeDetectsModifiedStagedUntrackedDeletedSpacesAndUnicode()
    {
        using var repository = InitializedRepository();
        repository.Write("tracked.txt", "modified");
        repository.Write("staged with spaces.txt", "staged");
        repository.Run("add", "--", "staged with spaces.txt");
        repository.Write("Unicode-ñ-文件.txt", "untracked");
        repository.Delete("delete-me.txt");

        var status = await client.GetWorkingTreeStatusAsync(repository.RootPath);

        Assert.True(status.HasStagedChanges);
        Assert.True(status.HasUnstagedChanges);
        Assert.True(status.HasUntrackedFiles);
        Assert.Contains(status.Entries, entry => entry.Path == "tracked.txt" && entry.WorkTreeState == GitFileState.Modified);
        Assert.Contains(status.Entries, entry => entry.Path == "staged with spaces.txt" && entry.IndexState == GitFileState.Added);
        Assert.Contains(status.Entries, entry => entry.Path == "Unicode-ñ-文件.txt" && entry.IsUntracked);
        Assert.Contains(status.Entries, entry => entry.Path == "delete-me.txt" && entry.WorkTreeState == GitFileState.Deleted);
    }

    [Fact]
    public async Task WorkingTreeRenamePreservesOriginalAndCurrentPaths()
    {
        using var repository = InitializedRepository();
        repository.Run("mv", "old name.txt", "new name ñ.txt");

        var entry = Assert.Single((await client.GetWorkingTreeStatusAsync(repository.RootPath)).Entries,
            candidate => candidate.IsRename);

        Assert.Equal("old name.txt", entry.OriginalPath);
        Assert.Equal("new name ñ.txt", entry.Path);
    }

    [Fact]
    public async Task ChangesBetweenCommitsAndAncestorCheckWork()
    {
        using var repository = InitializedRepository();
        var first = await client.ResolveCommitAsync(repository.RootPath, "HEAD");
        repository.Write("tracked.txt", "second");
        repository.Run("mv", "old name.txt", "renamed 文件.txt");
        repository.Write("added with spaces.txt", "added");
        repository.CommitAll("second");
        var second = await client.ResolveCommitAsync(repository.RootPath, "HEAD");

        var changes = await client.GetChangesAsync(repository.RootPath, first, second);

        Assert.Contains(changes, change => change.ChangeType == GitChangeType.Modified && change.Path == "tracked.txt");
        Assert.Contains(changes, change => change.ChangeType == GitChangeType.Added && change.Path == "added with spaces.txt");
        Assert.Contains(changes, change => change.ChangeType == GitChangeType.Renamed &&
            change.OriginalPath == "old name.txt" && change.Path == "renamed 文件.txt");
        Assert.True(await client.IsAncestorAsync(repository.RootPath, first, second));
        Assert.False(await client.IsAncestorAsync(repository.RootPath, second, first));
    }

    [Fact]
    public async Task DetachedHeadIsDetected()
    {
        using var repository = InitializedRepository();
        repository.Run("checkout", "--detach", "HEAD");

        var info = await client.GetRepositoryInfoAsync(repository.RootPath);

        Assert.True(info.IsHeadDetached);
        Assert.Null(info.Branch);
    }

    [Fact]
    public async Task MergeConflictIsDetected()
    {
        using var repository = InitializedRepository();
        repository.Run("checkout", "-b", "other");
        repository.Write("tracked.txt", "other");
        repository.CommitAll("other change");
        repository.Run("checkout", "main");
        repository.Write("tracked.txt", "main");
        repository.CommitAll("main change");
        repository.RunAllowFailure("merge", "--no-edit", "other");

        var status = await client.GetWorkingTreeStatusAsync(repository.RootPath);

        Assert.True(status.HasConflicts);
        Assert.Contains(status.Entries, entry => entry.Path == "tracked.txt" && entry.IsConflict);
    }

    [Fact]
    public async Task MissingReferenceAndNonRepositoryProduceTypedErrors()
    {
        using var repository = InitializedRepository();
        var missing = await Assert.ThrowsAsync<GitClientException>(() =>
            client.ResolveCommitAsync(repository.RootPath, "refs/heads/does-not-exist"));
        Assert.Equal(GitErrorKind.ReferenceNotFound, missing.Kind);

        using var directory = new TemporaryGitRepository(initialize: false);
        var notRepository = await Assert.ThrowsAsync<GitClientException>(() =>
            client.DiscoverRootAsync(directory.RootPath));
        Assert.Equal(GitErrorKind.NotARepository, notRepository.Kind);
    }

    [Fact]
    public async Task RepositoryWithoutInitialCommitProducesTypedError()
    {
        using var repository = new TemporaryGitRepository();

        var exception = await Assert.ThrowsAsync<GitClientException>(() =>
            client.GetRepositoryInfoAsync(repository.RootPath));

        Assert.Equal(GitErrorKind.NoInitialCommit, exception.Kind);
    }

    private static TemporaryGitRepository InitializedRepository()
    {
        var repository = new TemporaryGitRepository();
        repository.Write("tracked.txt", "initial");
        repository.Write("delete-me.txt", "delete");
        repository.Write("old name.txt", "rename");
        repository.CommitAll("initial");
        return repository;
    }
}
