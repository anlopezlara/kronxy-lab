using System.Buffers;
using System.Text;

namespace Kronxy.Context.Files;

public enum TextReadStatus
{
    Success,
    Binary,
    UnsupportedEncoding,
    TooLarge,
    ModifiedDuringRead,
    Missing,
    AccessDenied,
    ReparsePoint,
    InvalidPath,
    IoError
}

public sealed record SafeTextReadResult
{
    public required TextReadStatus Status { get; init; }
    public string? Content { get; init; }
    public bool IsSuccess => Status == TextReadStatus.Success;
}

public interface ISafeTextFileReader
{
    Task<SafeTextReadResult> ReadAsync(
        string repositoryRoot,
        string logicalPath,
        int maximumBytes,
        CancellationToken cancellationToken = default);
}

public sealed class SafeTextFileReader : ISafeTextFileReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly IRepositoryPathResolver resolver;
    private readonly IFileSystemAccess fileSystem;

    public SafeTextFileReader() : this(new RepositoryPathResolver(), new FileSystemAccess()) { }

    internal SafeTextFileReader(IRepositoryPathResolver resolver, IFileSystemAccess fileSystem)
    {
        this.resolver = resolver;
        this.fileSystem = fileSystem;
    }

    public async Task<SafeTextReadResult> ReadAsync(
        string repositoryRoot,
        string logicalPath,
        int maximumBytes,
        CancellationToken cancellationToken = default)
    {
        if (maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        var resolution = resolver.Resolve(repositoryRoot, logicalPath);
        if (!resolution.Success) return Result(TextReadStatus.InvalidPath);
        var physicalPath = resolution.PhysicalPath!;

        var security = RepositoryPhysicalPathSecurity.Inspect(repositoryRoot, physicalPath, fileSystem);
        var mapped = MapSecurity(security);
        if (mapped != TextReadStatus.Success) return Result(mapped);

        FileMetadata before;
        try { before = fileSystem.GetMetadata(physicalPath); }
        catch (UnauthorizedAccessException) { return Result(TextReadStatus.AccessDenied); }
        catch (FileNotFoundException) { return Result(TextReadStatus.Missing); }
        catch (DirectoryNotFoundException) { return Result(TextReadStatus.Missing); }
        catch (IOException) { return Result(TextReadStatus.IoError); }
        byte[]? rented = null;
        try
        {
            await using var stream = fileSystem.OpenRead(physicalPath);
            using var content = new MemoryStream(Math.Min(maximumBytes, 16_384));
            rented = ArrayPool<byte>.Shared.Rent(16_384);
            long observed = 0;
            while (true)
            {
                var permitted = Math.Min(rented.Length, (long)maximumBytes + 1 - observed);
                if (permitted <= 0) return Result(TextReadStatus.TooLarge);
                var read = await stream.ReadAsync(rented.AsMemory(0, (int)permitted), cancellationToken).ConfigureAwait(false);
                if (read == 0) break;
                observed += read;
                if (observed > maximumBytes) return Result(TextReadStatus.TooLarge);
                content.Write(rented, 0, read);
            }

            var afterSecurity = RepositoryPhysicalPathSecurity.Inspect(repositoryRoot, physicalPath, fileSystem);
            mapped = MapSecurity(afterSecurity);
            if (mapped != TextReadStatus.Success) return Result(mapped);
            var after = fileSystem.GetMetadata(physicalPath);
            if (before.Length != after.Length || before.LastWriteTimeUtc != after.LastWriteTimeUtc || observed != after.Length)
                return Result(TextReadStatus.ModifiedDuringRead);

            var bytes = content.ToArray();
            if (HasPrefix(bytes, 0xFF, 0xFE, 0x00, 0x00) || HasPrefix(bytes, 0x00, 0x00, 0xFE, 0xFF) ||
                HasPrefix(bytes, 0xFF, 0xFE) || HasPrefix(bytes, 0xFE, 0xFF))
                return Result(TextReadStatus.UnsupportedEncoding);
            if (Array.IndexOf(bytes, (byte)0) >= 0) return Result(TextReadStatus.Binary);
            try
            {
                var offset = HasPrefix(bytes, 0xEF, 0xBB, 0xBF) ? 3 : 0;
                return new SafeTextReadResult { Status = TextReadStatus.Success, Content = StrictUtf8.GetString(bytes, offset, bytes.Length - offset) };
            }
            catch (DecoderFallbackException) { return Result(TextReadStatus.UnsupportedEncoding); }
        }
        catch (UnauthorizedAccessException) { return Result(TextReadStatus.AccessDenied); }
        catch (FileNotFoundException) { return Result(TextReadStatus.Missing); }
        catch (DirectoryNotFoundException) { return Result(TextReadStatus.Missing); }
        catch (IOException) { return Result(TextReadStatus.IoError); }
        finally
        {
            if (rented is not null) ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }

    private static TextReadStatus MapSecurity(FileExclusionReason reason) => reason switch
    {
        FileExclusionReason.None => TextReadStatus.Success,
        FileExclusionReason.Missing => TextReadStatus.Missing,
        FileExclusionReason.AccessDenied => TextReadStatus.AccessDenied,
        FileExclusionReason.ReparsePoint => TextReadStatus.ReparsePoint,
        _ => TextReadStatus.IoError
    };

    private static SafeTextReadResult Result(TextReadStatus status) => new() { Status = status, Content = null };

    private static bool HasPrefix(byte[] bytes, params byte[] prefix) =>
        bytes.Length >= prefix.Length && prefix.Select((value, index) => bytes[index] == value).All(matches => matches);
}
