using System.Text;
using Kronxy.Context.Files;
using Xunit;

namespace Kronxy.Context.Tests.Files;

public sealed class SafeTextFileReaderTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "kronxy-reader-tests", Guid.NewGuid().ToString("N"));
    private readonly SafeTextFileReader reader = new();

    public SafeTextFileReaderTests() => Directory.CreateDirectory(root);

    [Theory]
    [InlineData("", "")]
    [InlineData("plain text", "plain text")]
    [InlineData("Unicode-ñ-文件", "Unicode-ñ-文件")]
    public async Task ReadsValidUtf8WithoutBom(string value, string expected)
    {
        WriteBytes("file.txt", new UTF8Encoding(false).GetBytes(value));
        var result = await reader.ReadAsync(root, "file.txt", 1_024);
        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Content);
    }

    [Fact]
    public async Task ReadsUtf8WithBomWithoutReturningBom()
    {
        WriteBytes("file.txt", [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("text")]);
        Assert.Equal("text", (await reader.ReadAsync(root, "file.txt", 100)).Content);
    }

    [Theory]
    [InlineData(new byte[] { 65, 0, 66 }, TextReadStatus.Binary)]
    [InlineData(new byte[] { 0xC3, 0x28 }, TextReadStatus.UnsupportedEncoding)]
    [InlineData(new byte[] { 0xFF, 0xFE, 65, 0 }, TextReadStatus.UnsupportedEncoding)]
    [InlineData(new byte[] { 0xFE, 0xFF, 0, 65 }, TextReadStatus.UnsupportedEncoding)]
    [InlineData(new byte[] { 0xFF, 0xFE, 0, 0, 65 }, TextReadStatus.UnsupportedEncoding)]
    [InlineData(new byte[] { 0, 0, 0xFE, 0xFF, 65 }, TextReadStatus.UnsupportedEncoding)]
    public async Task RejectsBinaryAndUnsupportedEncoding(byte[] bytes, TextReadStatus status)
    {
        WriteBytes("file.txt", bytes);
        var result = await reader.ReadAsync(root, "file.txt", 100);
        Assert.Equal(status, result.Status);
        Assert.Null(result.Content);
    }

    [Fact]
    public async Task AcceptsExactLimitAndRejectsOneByteMoreWithoutPartialContent()
    {
        WriteBytes("exact.txt", Encoding.UTF8.GetBytes("12345"));
        WriteBytes("large.txt", Encoding.UTF8.GetBytes("123456"));
        Assert.True((await reader.ReadAsync(root, "exact.txt", 5)).IsSuccess);
        var large = await reader.ReadAsync(root, "large.txt", 5);
        Assert.Equal(TextReadStatus.TooLarge, large.Status);
        Assert.Null(large.Content);
    }

    [Fact]
    public async Task ReadsFileThroughOrdinaryIntermediateDirectory()
    {
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        WriteBytes(Path.Combine("sub", "file.txt"), Encoding.UTF8.GetBytes("text"));
        Assert.True((await reader.ReadAsync(root, "sub/file.txt", 100)).IsSuccess);
    }

    [Fact]
    public async Task MissingAndTraversalReturnControlledResults()
    {
        Assert.Equal(TextReadStatus.Missing, (await reader.ReadAsync(root, "missing.txt", 100)).Status);
        Assert.Equal(TextReadStatus.InvalidPath, (await reader.ReadAsync(root, "../outside.txt", 100)).Status);
    }

    [Fact]
    public async Task MetadataChangeDuringReadIsDetectedDeterministically()
    {
        var fake = new SequencedFileSystem(Encoding.UTF8.GetBytes("safe"), changeMetadata: true);
        var result = await new SafeTextFileReader(new RepositoryPathResolver(), fake)
            .ReadAsync(root, "file.txt", 100);
        Assert.Equal(TextReadStatus.ModifiedDuringRead, result.Status);
        Assert.Null(result.Content);
    }

    [Fact]
    public async Task MultibyteUtf8SplitAcrossReadsIsDecodedAfterCompleteCapture()
    {
        var bytes = Encoding.UTF8.GetBytes("ñ文");
        var fake = new SequencedFileSystem(bytes, streamFactory: data => new OneByteAtATimeStream(data));
        var result = await new SafeTextFileReader(new RepositoryPathResolver(), fake)
            .ReadAsync(root, "file.txt", 100);
        Assert.True(result.IsSuccess);
        Assert.Equal("ñ文", result.Content);
    }

    [Fact]
    public async Task ReparseAndAccessDeniedAreTypedWithoutContent()
    {
        var reparse = new SequencedFileSystem([], attributes: FileAttributes.ReparsePoint);
        Assert.Equal(TextReadStatus.ReparsePoint,
            (await new SafeTextFileReader(new RepositoryPathResolver(), reparse).ReadAsync(root, "file", 10)).Status);
        var denied = new ThrowingFileSystem();
        var result = await new SafeTextFileReader(new RepositoryPathResolver(), denied).ReadAsync(root, "file", 10);
        Assert.Equal(TextReadStatus.AccessDenied, result.Status);
        Assert.Null(result.Content);
    }

    private void WriteBytes(string name, byte[] bytes)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
        var parent = Path.GetDirectoryName(root)!;
        if (Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent);
    }

    private sealed class SequencedFileSystem(
        byte[] content,
        bool changeMetadata = false,
        FileAttributes attributes = FileAttributes.Normal,
        Func<byte[], Stream>? streamFactory = null) : IFileSystemAccess
    {
        private bool opened;
        public FileMetadata GetMetadata(string path)
        {
            return new FileMetadata(true, content.Length, changeMetadata && opened
                ? DateTime.UnixEpoch.AddSeconds(1) : DateTime.UnixEpoch, attributes);
        }
        public Stream OpenRead(string path)
        {
            opened = true;
            return streamFactory?.Invoke(content) ?? new MemoryStream(content, writable: false);
        }
    }

    private sealed class OneByteAtATimeStream(byte[] content) : MemoryStream(content, writable: false)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
    }

    private sealed class ThrowingFileSystem : IFileSystemAccess
    {
        public FileMetadata GetMetadata(string path) => throw new UnauthorizedAccessException("controlled");
        public Stream OpenRead(string path) => throw new UnauthorizedAccessException("controlled");
    }
}
