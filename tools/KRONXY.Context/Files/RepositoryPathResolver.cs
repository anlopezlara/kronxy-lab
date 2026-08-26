namespace Kronxy.Context.Files;

internal enum PathResolutionError { None, Invalid, OutsideRepository }

internal sealed record PathResolutionResult(string? PhysicalPath, string? LogicalPath, PathResolutionError Error)
{
    public bool Success => Error == PathResolutionError.None;
}

internal interface IRepositoryPathResolver
{
    PathResolutionResult Resolve(string repositoryRoot, string logicalPath);
}

internal sealed class RepositoryPathResolver : IRepositoryPathResolver
{
    public PathResolutionResult Resolve(string repositoryRoot, string logicalPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot) || !Path.IsPathFullyQualified(repositoryRoot) ||
            string.IsNullOrEmpty(logicalPath) || logicalPath.IndexOfAny(['\0', '\r', '\n']) >= 0 ||
            IsRootedOnAnyPlatform(logicalPath) || IsDeviceOrUnc(logicalPath))
        {
            return Reject(PathResolutionError.Invalid);
        }

        var segments = logicalPath.Replace('\\', '/').Split('/');
        var invalidFileNameCharacters = Path.GetInvalidFileNameChars();
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".." ||
                                    segment.IndexOfAny(invalidFileNameCharacters) >= 0) ||
            OperatingSystem.IsWindows() && segments.Any(IsInvalidWindowsSegment))
        {
            return Reject(PathResolutionError.Invalid);
        }

        try
        {
            var root = Path.GetFullPath(repositoryRoot);
            var rootWithSeparator = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
            var physical = Path.GetFullPath(Path.Combine(root, Path.Combine(segments)));
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var relative = Path.GetRelativePath(root, physical).Replace('\\', '/');
            if (!physical.StartsWith(rootWithSeparator, comparison) ||
                relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            {
                return Reject(PathResolutionError.OutsideRepository);
            }

            return new PathResolutionResult(physical, string.Join('/', segments), PathResolutionError.None);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Reject(PathResolutionError.Invalid);
        }
    }

    private static bool IsRootedOnAnyPlatform(string path) =>
        Path.IsPathRooted(path) || path.StartsWith('/') ||
        path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';

    private static bool IsDeviceOrUnc(string path) =>
        path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal);

    private static bool IsInvalidWindowsSegment(string segment)
    {
        if (segment.Contains(':') || segment.EndsWith(' ') || segment.EndsWith('.')) return true;
        var baseName = segment.Split('.')[0].TrimEnd(' ', '.');
        return baseName.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
               baseName.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
               baseName.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
               baseName.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
               baseName.Length == 4 &&
               (baseName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                baseName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
               baseName[3] is >= '1' and <= '9';
    }

    private static PathResolutionResult Reject(PathResolutionError error) => new(null, null, error);
}
