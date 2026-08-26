using Kronxy.Context.Git;
using Xunit;

namespace Kronxy.Context.Tests.Git;

public sealed class GitIndexParserTests
{
    private const string Sha1 = "0123456789012345678901234567890123456789";

    [Theory]
    [InlineData("100644", GitIndexEntryType.RegularFile)]
    [InlineData("100755", GitIndexEntryType.ExecutableFile)]
    [InlineData("120000", GitIndexEntryType.SymbolicLink)]
    [InlineData("160000", GitIndexEntryType.GitLink)]
    [InlineData("100600", GitIndexEntryType.Unknown)]
    public void IndexParser_ClassifiesModes(string mode, GitIndexEntryType expected)
    {
        var entry = Assert.Single(GitIndexParser.Parse($"{mode} {Sha1} 0\tfile.txt\0"));
        Assert.Equal(expected, entry.EntryType);
    }

    [Fact]
    public void IndexParser_PreservesStageAndDifficultPath()
    {
        const string path = "-folder/Unicode-ñ\tline\nname.txt";
        var entry = Assert.Single(GitIndexParser.Parse($"100644 {new string('A', 64)} 3\t{path}\0"));
        Assert.Equal(path, entry.Path);
        Assert.Equal(3, entry.Stage);
        Assert.True(entry.IsConflict);
        Assert.Equal(new string('a', 64), entry.ObjectId);
    }

    [Theory]
    [InlineData("100644 0123456789012345678901234567890123456789 0\tfile.txt")]
    [InlineData("100644 0123 0\tfile.txt\0")]
    [InlineData("100644 0123456789012345678901234567890123456789 4\tfile.txt\0")]
    [InlineData("100644 0123456789012345678901234567890123456789 0 file.txt\0")]
    public void IndexParser_RejectsMalformedRecords(string output) =>
        Assert.Equal(GitErrorKind.MalformedOutput,
            Assert.Throws<GitClientException>(() => GitIndexParser.Parse(output)).Kind);

    [Fact]
    public void PathListParser_PreservesNulDelimitedDifficultPaths()
    {
        var paths = GitPathListParser.Parse("space name.txt\0Unicode-ñ.txt\0-tab\tline\n.txt\0");
        Assert.Equal(["space name.txt", "Unicode-ñ.txt", "-tab\tline\n.txt"], paths);
    }

    [Theory]
    [InlineData("file.txt")]
    [InlineData("\0")]
    public void PathListParser_RejectsMalformedOutput(string output) =>
        Assert.Throws<GitClientException>(() => GitPathListParser.Parse(output));
}
