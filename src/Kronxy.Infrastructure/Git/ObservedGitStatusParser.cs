using Kronxy.Application.Repositories;

namespace Kronxy.Infrastructure.Git;

internal static class ObservedGitStatusParser
{
    public static RepositoryOperationResult<IReadOnlyList<ObservedRepositoryChange>>
        Parse(string output)
    {
        if (string.IsNullOrEmpty(output))
            return Success([]);
        if (output[^1] != '\0')
            return Failure("REPOSITORY_OBSERVED_STATUS_TRUNCATED");

        string[] records = output.Split('\0');
        var changes = new List<ObservedRepositoryChange>();
        var paths = new HashSet<string>(PathComparer);
        for (int index = 0; index < records.Length - 1; index++)
        {
            string record = records[index];
            if (record.Length < 4 || record[2] != ' ')
                return Failure("REPOSITORY_OBSERVED_STATUS_INVALID");
            char x = record[0];
            char y = record[1];
            if (x is 'R' or 'C' || y is 'R' or 'C' ||
                x is 'U' || y is 'U')
                return Failure("REPOSITORY_OBSERVED_CHANGE_UNSUPPORTED");

            string path = record[3..].Replace('\\', '/');
            if (!IsSafePath(path) || !paths.Add(path))
                return Failure("REPOSITORY_OBSERVED_PATH_INVALID");

            ObservedRepositoryChangeKind? kind = Map(x, y);
            if (kind is null)
                return Failure("REPOSITORY_OBSERVED_CHANGE_UNSUPPORTED");
            changes.Add(new ObservedRepositoryChange(kind.Value, path));
        }
        return Success(Array.AsReadOnly(changes.ToArray()));
    }

    private static ObservedRepositoryChangeKind? Map(char x, char y)
    {
        if (x == '?' && y == '?') return ObservedRepositoryChangeKind.Created;
        if (x == '!' && y == '!') return null;
        bool added = x == 'A' || y == 'A';
        bool deleted = x == 'D' || y == 'D';
        bool modified = x == 'M' || y == 'M';
        if ((added && deleted) || (added && !modified && x != 'A' && y != 'A')) return null;
        if (deleted) return ObservedRepositoryChangeKind.Deleted;
        if (added) return ObservedRepositoryChangeKind.Created;
        if (modified) return ObservedRepositoryChangeKind.Modified;
        return null;
    }

    private static bool IsSafePath(string path) =>
        !string.IsNullOrWhiteSpace(path) &&
        !Path.IsPathFullyQualified(path) &&
        !path.Contains(':') &&
        path.Split('/').All(segment =>
            !string.IsNullOrWhiteSpace(segment) && segment is not "." and not "..");

    private static RepositoryOperationResult<IReadOnlyList<ObservedRepositoryChange>> Success(
        IReadOnlyList<ObservedRepositoryChange> changes) =>
        RepositoryOperationResult<IReadOnlyList<ObservedRepositoryChange>>.Success(changes);

    private static RepositoryOperationResult<IReadOnlyList<ObservedRepositoryChange>> Failure(string code) =>
        RepositoryOperationResult<IReadOnlyList<ObservedRepositoryChange>>.Failure(
            RepositoryFailureKind.UnsupportedObservedChange, code);

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
