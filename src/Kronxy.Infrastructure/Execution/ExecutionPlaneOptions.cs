namespace Kronxy.Infrastructure.Execution;

public sealed record ExecutionPlaneOptions
{
    public const string SectionName =
        "ExecutionPlane";

    public required string RepositoryRoot
    {
        get;
        init;
    }

    public required string WorkspaceRoot
    {
        get;
        init;
    }

    public required string DotnetExecutable
    {
        get;
        init;
    }

    public required string GitExecutable
    {
        get;
        init;
    }

    public required string DotnetTarget
    {
        get;
        init;
    }

    public void Validate()
    {
        ValidateAbsolutePath(
            RepositoryRoot,
            nameof(RepositoryRoot),
            requireDirectory: true);

        ValidateAbsolutePath(
            WorkspaceRoot,
            nameof(WorkspaceRoot),
            requireDirectory: false);

        ValidateAbsolutePath(
            DotnetExecutable,
            nameof(DotnetExecutable),
            requireFile: true);

        ValidateAbsolutePath(
            GitExecutable,
            nameof(GitExecutable),
            requireFile: true);

        ValidateDotnetTarget(
            DotnetTarget);

        string repository =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(
                    RepositoryRoot));

        string workspace =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(
                    WorkspaceRoot));

        if (PathEquals(
                repository,
                workspace))
        {
            throw new InvalidOperationException(
                "ExecutionPlane RepositoryRoot and WorkspaceRoot " +
                "must be different paths.");
        }

        if (IsUnderRoot(
                repository,
                workspace))
        {
            throw new InvalidOperationException(
                "ExecutionPlane WorkspaceRoot must not be inside " +
                "the authoritative repository.");
        }
    }

    private static void ValidateDotnetTarget(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.IndexOfAny(
                [
                    '\0',
                    '\r',
                    '\n',
                    ';',
                    '&',
                    '|',
                    '<',
                    '>',
                    '$',
                    '`',
                    '"',
                    '\''
                ]) >= 0 ||
            Path.IsPathFullyQualified(value) ||
            value.StartsWith('/') ||
            value.StartsWith('\\'))
        {
            throw new InvalidOperationException(
                "ExecutionPlane DotnetTarget is invalid.");
        }

        string[] segments =
            value
                .Replace('\\', '/')
                .Split('/');

        if (segments.Any(
                segment =>
                    string.IsNullOrWhiteSpace(segment) ||
                    segment is "." or ".."))
        {
            throw new InvalidOperationException(
                "ExecutionPlane DotnetTarget is invalid.");
        }

        string extension =
            Path.GetExtension(value);

        if (!extension.Equals(
                ".sln",
                StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(
                ".csproj",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "ExecutionPlane DotnetTarget type is not allowed.");
        }
    }

    private static void ValidateAbsolutePath(
        string? value,
        string name,
        bool requireDirectory = false,
        bool requireFile = false)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.IndexOfAny(
                ['\0', '\r', '\n']) >= 0 ||
            !Path.IsPathFullyQualified(value))
        {
            throw new InvalidOperationException(
                $"ExecutionPlane {name} is invalid.");
        }

        string fullPath;

        try
        {
            fullPath =
                Path.GetFullPath(value);
        }
        catch (Exception exception)
            when (
                exception is ArgumentException or
                NotSupportedException or
                PathTooLongException)
        {
            throw new InvalidOperationException(
                $"ExecutionPlane {name} is invalid.",
                exception);
        }

        if (requireDirectory &&
            !Directory.Exists(fullPath))
        {
            throw new InvalidOperationException(
                $"ExecutionPlane {name} directory does not exist.");
        }

        if (requireFile &&
            !File.Exists(fullPath))
        {
            throw new InvalidOperationException(
                $"ExecutionPlane {name} executable does not exist.");
        }
    }

    private static bool PathEquals(
        string left,
        string right)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(right)),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
    }

    private static bool IsUnderRoot(
        string root,
        string candidate)
    {
        string normalizedRoot =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(root));

        string normalizedCandidate =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(candidate));

        string prefix =
            normalizedRoot +
            Path.DirectorySeparatorChar;

        return normalizedCandidate.StartsWith(
            prefix,
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
    }
}
