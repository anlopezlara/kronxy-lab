namespace Kronxy.Context.Git;

internal static class GitDiffParser
{
    public static IReadOnlyList<GitChange> Parse(string output)
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

        var fields = output.Split('\0');
        var changes = new List<GitChange>();
        for (var index = 0; index < fields.Length - 1;)
        {
            var status = fields[index++];
            if (status.Length == 0 || index >= fields.Length - 1)
            {
                throw Malformed();
            }

            var type = ParseType(status[0]);
            ValidateStatus(status, type);
            if (type is GitChangeType.Renamed or GitChangeType.Copied)
            {
                if (index + 1 >= fields.Length - 1)
                {
                    throw Malformed();
                }

                var original = RequirePath(fields[index++]);
                var current = RequirePath(fields[index++]);
                changes.Add(new GitChange { ChangeType = type, Path = current, OriginalPath = original });
            }
            else
            {
                changes.Add(new GitChange { ChangeType = type, Path = RequirePath(fields[index++]) });
            }
        }

        return changes;
    }

    private static GitChangeType ParseType(char value) => value switch
    {
        'A' => GitChangeType.Added,
        'M' => GitChangeType.Modified,
        'D' => GitChangeType.Deleted,
        'R' => GitChangeType.Renamed,
        'C' => GitChangeType.Copied,
        'T' => GitChangeType.TypeChanged,
        _ => GitChangeType.Unknown
    };

    private static void ValidateStatus(string status, GitChangeType type)
    {
        if (type is GitChangeType.Renamed or GitChangeType.Copied)
        {
            if (status.Length is < 2 or > 4 ||
                !status[1..].All(char.IsAsciiDigit) ||
                !int.TryParse(status[1..], out var score) ||
                score is < 0 or > 100)
            {
                throw Malformed();
            }

            return;
        }

        if (status.Length != 1)
        {
            throw Malformed();
        }
    }

    private static string RequirePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Contains('\0'))
        {
            throw Malformed();
        }

        return path;
    }

    private static GitClientException Malformed() =>
        new(GitErrorKind.MalformedOutput, "Git devolvió diferencias estructuradas no válidas.");
}
