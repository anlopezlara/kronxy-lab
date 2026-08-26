namespace Kronxy.Context.Git;

internal static class GitStatusParser
{
    public static WorkingTreeStatus Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (output.Length == 0)
        {
            return new WorkingTreeStatus();
        }

        RequireNulTermination(output);
        var records = output.Split('\0');
        var entries = new List<WorkingTreeEntry>();

        for (var index = 0; index < records.Length - 1; index++)
        {
            var record = records[index];
            if (record.Length < 2)
            {
                throw Malformed();
            }

            switch (record[0])
            {
                case '?':
                    RequirePathRecordPrefix(record);
                    entries.Add(new WorkingTreeEntry
                    {
                        Path = RequirePath(record[2..]),
                        IsUntracked = true,
                        IndexState = GitFileState.Unmodified,
                        WorkTreeState = GitFileState.Unmodified
                    });
                    break;
                case '1':
                    entries.Add(ParseTracked(record, 9, hasOriginalPath: false, null));
                    break;
                case '2':
                    if (++index >= records.Length - 1)
                    {
                        throw Malformed();
                    }

                    entries.Add(ParseTracked(record, 10, hasOriginalPath: true, records[index]));
                    break;
                case 'u':
                    entries.Add(ParseConflict(record));
                    break;
                case '!':
                    RequirePathRecordPrefix(record);
                    break;
                default:
                    throw Malformed();
            }
        }

        return new WorkingTreeStatus { Entries = entries };
    }

    private static WorkingTreeEntry ParseTracked(
        string record,
        int fieldCount,
        bool hasOriginalPath,
        string? originalPath)
    {
        var fields = record.Split(' ', fieldCount, StringSplitOptions.None);
        if (fields.Length != fieldCount || fields[1].Length != 2 ||
            hasOriginalPath && !IsRenameOrCopyScore(fields[8]))
        {
            throw Malformed();
        }

        return new WorkingTreeEntry
        {
            Path = RequirePath(fields[^1]),
            OriginalPath = hasOriginalPath ? RequirePath(originalPath!) : null,
            IndexState = ParseState(fields[1][0]),
            WorkTreeState = ParseState(fields[1][1])
        };
    }

    private static WorkingTreeEntry ParseConflict(string record)
    {
        var fields = record.Split(' ', 11, StringSplitOptions.None);
        if (fields.Length != 11 || fields[1].Length != 2)
        {
            throw Malformed();
        }

        return new WorkingTreeEntry
        {
            Path = RequirePath(fields[^1]),
            IndexState = GitFileState.Unmerged,
            WorkTreeState = GitFileState.Unmerged,
            IsConflict = true
        };
    }

    private static GitFileState ParseState(char value) => value switch
    {
        '.' => GitFileState.Unmodified,
        'M' => GitFileState.Modified,
        'A' => GitFileState.Added,
        'D' => GitFileState.Deleted,
        'R' => GitFileState.Renamed,
        'C' => GitFileState.Copied,
        'T' => GitFileState.TypeChanged,
        'U' => GitFileState.Unmerged,
        _ => GitFileState.Unknown
    };

    private static bool IsRenameOrCopyScore(string value) =>
        value.Length is >= 2 and <= 4 &&
        value[0] is 'R' or 'C' &&
        value[1..].All(char.IsAsciiDigit) &&
        int.TryParse(value[1..], out var score) && score is >= 0 and <= 100;

    private static void RequirePathRecordPrefix(string record)
    {
        if (record.Length < 3 || record[1] != ' ')
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

    private static void RequireNulTermination(string output)
    {
        if (output[^1] != '\0')
        {
            throw Malformed();
        }
    }

    private static GitClientException Malformed() =>
        new(GitErrorKind.MalformedOutput, "Git devolvió un estado estructurado no válido.");
}
