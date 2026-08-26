using System.Text.RegularExpressions;

namespace Kronxy.Context.Git;

internal static partial class GitIndexParser
{
    public static IReadOnlyList<GitIndexEntry> Parse(string output)
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

        var entries = new List<GitIndexEntry>();
        foreach (var record in output.Split('\0')[..^1])
        {
            var tab = record.IndexOf('\t');
            if (tab <= 0 || tab == record.Length - 1)
            {
                throw Malformed();
            }

            var metadata = record[..tab].Split(' ', StringSplitOptions.None);
            var path = record[(tab + 1)..];
            if (metadata.Length != 3 || metadata.Any(string.IsNullOrEmpty) ||
                !ModeRegex().IsMatch(metadata[0]) || !ObjectIdRegex().IsMatch(metadata[1]) ||
                metadata[1].Length is not 40 and not 64 ||
                !int.TryParse(metadata[2], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var stage) || stage is < 0 or > 3 ||
                string.IsNullOrEmpty(path) || path.Contains('\0'))
            {
                throw Malformed();
            }

            entries.Add(new GitIndexEntry
            {
                Path = path,
                Mode = metadata[0],
                ObjectId = metadata[1].ToLowerInvariant(),
                Stage = stage,
                EntryType = metadata[0] switch
                {
                    "100644" => GitIndexEntryType.RegularFile,
                    "100755" => GitIndexEntryType.ExecutableFile,
                    "120000" => GitIndexEntryType.SymbolicLink,
                    "160000" => GitIndexEntryType.GitLink,
                    _ => GitIndexEntryType.Unknown
                }
            });
        }

        return entries;
    }

    [GeneratedRegex("\\A[0-7]{6}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex ModeRegex();

    [GeneratedRegex("\\A[0-9a-fA-F]+\\z", RegexOptions.CultureInvariant)]
    private static partial Regex ObjectIdRegex();

    private static GitClientException Malformed() =>
        new(GitErrorKind.MalformedOutput, "Git devolvió entradas de índice no válidas.");
}
