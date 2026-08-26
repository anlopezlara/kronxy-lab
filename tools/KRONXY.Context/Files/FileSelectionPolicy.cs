namespace Kronxy.Context.Files;

public interface IFileSelectionPolicy
{
    FileSelectionDecision Evaluate(
        RepositoryFileCandidate candidate,
        RepositoryInventoryLimits limits,
        string outputDirectory);
}

public sealed class FileSelectionPolicy : IFileSelectionPolicy
{
    private static readonly string[] ProhibitedDirectories =
    [
        ".git", ".vs", ".idea", ".vscode", "bin", "obj", "artifacts", "packages", "node_modules",
        "TestResults", "coverage", ".coverage", "tmp", "temp", ".tmp", ".temp", ".kronxy-context"
    ];

    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".pdb", ".obj", ".lib", ".so", ".dylib", ".db", ".sqlite", ".zip", ".7z",
        ".rar", ".tar", ".gz", ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".webp", ".mp3",
        ".wav", ".mp4", ".mov", ".avi", ".woff", ".woff2", ".ttf", ".otf", ".doc", ".docx", ".xls",
        ".xlsx", ".ppt", ".pptx", ".pdf", ".coverage"
    };

    private static readonly HashSet<string> SensitiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pfx", ".p12", ".jks", ".keystore", ".key", ".pem", ".cer", ".crt", ".der", ".snk", ".user"
    };

    public FileSelectionDecision Evaluate(
        RepositoryFileCandidate candidate,
        RepositoryInventoryLimits limits,
        string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(limits);
        var path = candidate.LogicalPath.Replace('\\', '/');
        if (path.Length > limits.MaxLogicalPathLength)
        {
            return Exclude(FileExclusionReason.InvalidPath);
        }

        if (candidate.IsConflict) return Exclude(FileExclusionReason.Conflict);
        if (candidate.IsSymbolicLink) return Exclude(FileExclusionReason.SymbolicLink);
        if (candidate.IsGitLink) return Exclude(FileExclusionReason.GitLink);
        if (candidate.GitEntryType == Git.GitIndexEntryType.Unknown) return Exclude(FileExclusionReason.UnknownGitType);
        var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (path.Split('/').SkipLast(1).Any(segment =>
                ProhibitedDirectories.Any(directory => segment.Equals(directory, pathComparison))))
            return Exclude(FileExclusionReason.ProhibitedDirectory);
        var normalizedOutputDirectory = (outputDirectory ?? string.Empty).Replace('\\', '/').Trim('/');
        if (normalizedOutputDirectory.Length > 0 &&
            (path.Equals(normalizedOutputDirectory, pathComparison) ||
             path.StartsWith(normalizedOutputDirectory + "/", pathComparison)))
            return Exclude(FileExclusionReason.ProhibitedDirectory);

        var name = Path.GetFileName(path);
        if (IsSensitiveName(name)) return Exclude(FileExclusionReason.SensitiveName);
        if (BinaryExtensions.Contains(Path.GetExtension(name))) return Exclude(FileExclusionReason.BinaryExtension);
        return new FileSelectionDecision { Include = true, Reason = FileExclusionReason.None };
    }

    private static bool IsSensitiveName(string name)
    {
        if (name.Equals(".env.example", StringComparison.OrdinalIgnoreCase) ||
            name.Equals(".env.sample", StringComparison.OrdinalIgnoreCase) ||
            name.Equals(".env.template", StringComparison.OrdinalIgnoreCase)) return false;
        if (name.Equals(".env", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith(".env.", StringComparison.OrdinalIgnoreCase)) return true;
        if (SensitiveFileNames.Contains(name)) return true;
        if (SensitiveExtensions.Contains(Path.GetExtension(name))) return true;
        return name.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("privatekey", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("secrets.json", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly HashSet<string> SensitiveFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "credentials.json", "secrets.json", "id_rsa", "id_ed25519", ".npmrc", ".pypirc"
    };

    private static FileSelectionDecision Exclude(FileExclusionReason reason) => new() { Include = false, Reason = reason };
}
