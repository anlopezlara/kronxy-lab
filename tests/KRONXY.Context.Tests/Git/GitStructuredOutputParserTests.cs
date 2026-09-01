using Kronxy.Context.Git;
using Xunit;

namespace Kronxy.Context.Tests.Git;

public sealed class GitStructuredOutputParserTests
{
    [Fact]
    public void ParseIndexEntries_uses_strict_existing_parser()
    {
        string output =
            "100644 " +
            new string('a', 40) +
            " 0\tsrc/file.cs\0";

        var entries =
            GitStructuredOutputParser
                .ParseIndexEntries(output);

        var entry =
            Assert.Single(entries);

        Assert.Equal(
            "src/file.cs",
            entry.Path);

        Assert.Equal(
            GitIndexEntryType.RegularFile,
            entry.EntryType);

        Assert.Equal(
            0,
            entry.Stage);
    }

    [Fact]
    public void ParsePathList_uses_strict_existing_parser()
    {
        var paths =
            GitStructuredOutputParser
                .ParsePathList(
                    "one.cs\0two.cs\0");

        Assert.Equal(
            ["one.cs", "two.cs"],
            paths);
    }

    [Fact]
    public void ParseIndexEntries_rejects_non_nul_terminated_output()
    {
        Assert.Throws<GitClientException>(
            () =>
                GitStructuredOutputParser
                    .ParseIndexEntries(
                        "100644 " +
                        new string('a', 40) +
                        " 0\tsrc/file.cs"));
    }

    [Fact]
    public void ParsePathList_rejects_non_nul_terminated_output()
    {
        Assert.Throws<GitClientException>(
            () =>
                GitStructuredOutputParser
                    .ParsePathList(
                        "file.cs"));
    }
}
