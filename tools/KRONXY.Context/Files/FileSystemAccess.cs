namespace Kronxy.Context.Files;

internal readonly record struct FileMetadata(bool Exists, long Length, DateTime LastWriteTimeUtc, FileAttributes Attributes);

internal interface IFileSystemAccess
{
    FileMetadata GetMetadata(string path);
    Stream OpenRead(string path);
}

internal sealed class FileSystemAccess : IFileSystemAccess
{
    public FileMetadata GetMetadata(string path)
    {
        if (Directory.Exists(path))
        {
            var directory = new DirectoryInfo(path);
            directory.Refresh();
            return new FileMetadata(true, 0, directory.LastWriteTimeUtc, directory.Attributes);
        }

        var info = new FileInfo(path);
        info.Refresh();
        return new FileMetadata(info.Exists, info.Exists ? info.Length : 0, info.LastWriteTimeUtc, info.Attributes);
    }

    public Stream OpenRead(string path) => new FileStream(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        bufferSize: 16_384,
        FileOptions.Asynchronous | FileOptions.SequentialScan);
}

internal static class RepositoryPhysicalPathSecurity
{
    public static FileExclusionReason Inspect(
        string repositoryRoot,
        string physicalPath,
        IFileSystemAccess fileSystem)
    {
        var root = Path.GetFullPath(repositoryRoot);
        try
        {
            var rootMetadata = fileSystem.GetMetadata(root);
            if (!rootMetadata.Exists) return FileExclusionReason.Missing;
            if ((rootMetadata.Attributes & FileAttributes.ReparsePoint) != 0)
                return FileExclusionReason.ReparsePoint;
        }
        catch (UnauthorizedAccessException) { return FileExclusionReason.AccessDenied; }
        catch (IOException) { return FileExclusionReason.IoError; }

        var relative = Path.GetRelativePath(root, physicalPath);
        var current = root;
        foreach (var segment in relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileMetadata metadata;
            try
            {
                metadata = fileSystem.GetMetadata(current);
            }
            catch (UnauthorizedAccessException)
            {
                return FileExclusionReason.AccessDenied;
            }
            catch (IOException)
            {
                return FileExclusionReason.IoError;
            }

            if (!metadata.Exists) return FileExclusionReason.Missing;
            if ((metadata.Attributes & FileAttributes.ReparsePoint) != 0) return FileExclusionReason.ReparsePoint;
        }

        try
        {
            var final = fileSystem.GetMetadata(physicalPath);
            if ((final.Attributes & FileAttributes.Directory) != 0) return FileExclusionReason.Directory;
        }
        catch (UnauthorizedAccessException) { return FileExclusionReason.AccessDenied; }
        catch (IOException) { return FileExclusionReason.IoError; }
        return FileExclusionReason.None;
    }
}
