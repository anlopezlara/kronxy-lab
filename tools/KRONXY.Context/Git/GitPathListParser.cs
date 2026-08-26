namespace Kronxy.Context.Git;

internal static class GitPathListParser
{
    public static IReadOnlyList<string> Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (output.Length == 0)
        {
            return [];
        }

        if (output[^1] != '\0')
        {
            throw Malformed();
        }

        var paths = output.Split('\0')[..^1];
        if (paths.Any(string.IsNullOrEmpty))
        {
            throw Malformed();
        }

        return paths;
    }

    private static GitClientException Malformed() =>
        new(GitErrorKind.MalformedOutput, "Git devolvió una lista de rutas no válida.");
}
