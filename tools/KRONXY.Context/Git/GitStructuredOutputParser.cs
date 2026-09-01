namespace Kronxy.Context.Git;

public static class GitStructuredOutputParser
{
    public static IReadOnlyList<GitIndexEntry>
        ParseIndexEntries(
            string output)
    {
        return GitIndexParser.Parse(output);
    }

    public static IReadOnlyList<string>
        ParsePathList(
            string output)
    {
        return GitPathListParser.Parse(output);
    }
}
