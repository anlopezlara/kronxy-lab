using Kronxy.Context.Git;
using Xunit;

namespace Kronxy.Context.Tests.Git;

public sealed class GitParserTests
{
    private const string Hash = "0123456789012345678901234567890123456789";

    [Fact]
    public void StatusParser_ParsesOrdinaryStagedUnstagedAndUntrackedEntries()
    {
        var output =
            $"1 M. N... 100644 100644 100644 {Hash} {Hash} path with spaces.cs\0" +
            $"1 .D N... 100644 100644 000000 {Hash} {Hash} eliminado.txt\0" +
            "? carpeta/Unicode-ñ-文件.txt\0";

        var status = GitStatusParser.Parse(output);

        Assert.Equal(3, status.Entries.Count);
        Assert.True(status.HasStagedChanges);
        Assert.True(status.HasUnstagedChanges);
        Assert.True(status.HasUntrackedFiles);
        Assert.False(status.HasConflicts);
        Assert.Equal("path with spaces.cs", status.Entries[0].Path);
        Assert.Equal(GitFileState.Modified, status.Entries[0].IndexState);
        Assert.Equal(GitFileState.Deleted, status.Entries[1].WorkTreeState);
        Assert.True(status.Entries[2].IsUntracked);
    }

    [Fact]
    public void StatusParser_PreservesBothRenamePaths()
    {
        var output = $"2 R. N... 100644 100644 100644 {Hash} {Hash} R100 nueva ruta.txt\0ruta anterior.txt\0";
        var entry = Assert.Single(GitStatusParser.Parse(output).Entries);

        Assert.True(entry.IsRename);
        Assert.Equal("nueva ruta.txt", entry.Path);
        Assert.Equal("ruta anterior.txt", entry.OriginalPath);
    }

    [Fact]
    public void StatusParser_PreservesDifficultNulDelimitedPathsAndSimultaneousRenameState()
    {
        const string difficultPath = "-actual\tname\nlooks -> renamed.txt";
        const string originalPath = "-original\tname\nUnicode-ñ.txt";
        var output = $"2 RM N... 100644 100644 100644 {Hash} {Hash} R087 {difficultPath}\0{originalPath}\0";

        var entry = Assert.Single(GitStatusParser.Parse(output).Entries);

        Assert.Equal(difficultPath, entry.Path);
        Assert.Equal(originalPath, entry.OriginalPath);
        Assert.Equal(GitFileState.Renamed, entry.IndexState);
        Assert.Equal(GitFileState.Modified, entry.WorkTreeState);
    }

    [Fact]
    public void StatusParser_ParsesCopyScoreAndPaths()
    {
        var output = $"2 C. N... 100644 100644 100644 {Hash} {Hash} C100 copy.txt\0source.txt\0";
        var entry = Assert.Single(GitStatusParser.Parse(output).Entries);

        Assert.True(entry.IsCopy);
        Assert.Equal("source.txt", entry.OriginalPath);
        Assert.Equal("copy.txt", entry.Path);
    }

    [Fact]
    public void StatusParser_ParsesConflict()
    {
        var output = $"u UU N... 100644 100644 100644 100644 {Hash} {Hash} {Hash} conflicto.txt\0";
        var status = GitStatusParser.Parse(output);

        Assert.True(status.HasConflicts);
        Assert.Equal(GitFileState.Unmerged, Assert.Single(status.Entries).IndexState);
    }

    [Fact]
    public void StatusParser_EmptyOutputIsClean() => Assert.True(GitStatusParser.Parse(string.Empty).IsClean);

    [Theory]
    [InlineData("1 M. malformed")]
    [InlineData("x unknown\0")]
    [InlineData("2 R. N... 100644 100644 100644 a b R100 path\0")]
    [InlineData("?missing-space\0")]
    [InlineData("!missing-space\0")]
    [InlineData("2 R. N... 100644 100644 100644 a b Rbad path\0original\0")]
    public void StatusParser_MalformedOutputThrowsTypedError(string output)
    {
        var exception = Assert.Throws<GitClientException>(() => GitStatusParser.Parse(output));
        Assert.Equal(GitErrorKind.MalformedOutput, exception.Kind);
    }

    [Fact]
    public void DiffParser_ParsesAllTypesAndRenamePaths()
    {
        var output = "A\0added.txt\0M\0path with spaces.txt\0D\0deleted.txt\0" +
                     "R098\0old name.txt\0new name.txt\0C100\0origen-ñ.txt\0copia-文件.txt\0T\0typed.txt\0X\0unknown.txt\0";
        var changes = GitDiffParser.Parse(output);

        Assert.Equal(7, changes.Count);
        Assert.Equal(GitChangeType.Added, changes[0].ChangeType);
        Assert.Equal(GitChangeType.Modified, changes[1].ChangeType);
        Assert.Equal(GitChangeType.Deleted, changes[2].ChangeType);
        Assert.Equal(GitChangeType.Renamed, changes[3].ChangeType);
        Assert.Equal("old name.txt", changes[3].OriginalPath);
        Assert.Equal("new name.txt", changes[3].Path);
        Assert.Equal(GitChangeType.Copied, changes[4].ChangeType);
        Assert.Equal(GitChangeType.TypeChanged, changes[5].ChangeType);
        Assert.Equal(GitChangeType.Unknown, changes[6].ChangeType);
    }

    [Fact]
    public void DiffParser_PreservesDifficultNulDelimitedPaths()
    {
        const string original = "-old\tline\nlooks -> old.txt";
        const string current = "-new\tline\nUnicode-文件.txt";

        var change = Assert.Single(GitDiffParser.Parse($"R075\0{original}\0{current}\0"));

        Assert.Equal(original, change.OriginalPath);
        Assert.Equal(current, change.Path);
    }

    [Theory]
    [InlineData("M\0path-without-final-nul")]
    [InlineData("R100\0only-old\0")]
    [InlineData("\0")]
    [InlineData("Rbad\0old\0new\0")]
    [InlineData("C101\0old\0new\0")]
    [InlineData("Mjunk\0path\0")]
    public void DiffParser_MalformedOutputThrowsTypedError(string output)
    {
        var exception = Assert.Throws<GitClientException>(() => GitDiffParser.Parse(output));
        Assert.Equal(GitErrorKind.MalformedOutput, exception.Kind);
    }
}
