using System.IO.Compression;
using System.Text;
using Kronxy.Context.Packaging;
using Kronxy.Context.Redaction;
using Xunit;

namespace Kronxy.Context.Tests.Packaging;

public sealed class PackageIntegrityAndAtomicityTests
{
    [Fact]
    public async Task VerifierAcceptsValidPackageAndRejectsTruncation()
    {
        using var folder = new TestFolder();
        var destination = folder.PathOf("valid.zip");
        Assert.True((await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(destination, PackageBuilderTests.Entry("a.txt", "safe")))).IsSuccess);
        var fixture = await FixtureAsync(destination);
        var verifier = new PackageIntegrityVerifier();
        Assert.True(await verifier.VerifyAsync(destination, fixture.Entries, fixture.Manifest, new PackageBuildLimits(), default));
        var bytes = await File.ReadAllBytesAsync(destination);
        await File.WriteAllBytesAsync(folder.PathOf("truncated.zip"), bytes[..^8]);
        Assert.False(await verifier.VerifyAsync(folder.PathOf("truncated.zip"), fixture.Entries, fixture.Manifest, new PackageBuildLimits(), default));
    }

    [Theory]
    [InlineData("extra")]
    [InlineData("missing")]
    [InlineData("content")]
    [InlineData("manifest")]
    [InlineData("duplicate-manifest")]
    public async Task VerifierRejectsAlteredStructureContentAndManifest(string mutation)
    {
        using var folder = new TestFolder();
        var original = folder.PathOf("original.zip");
        Assert.True((await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(original, PackageBuilderTests.Entry("a.txt", "safe")))).IsSuccess);
        var fixture = await FixtureAsync(original);
        var altered = folder.PathOf("altered.zip");
        using (var archive = ZipFile.Open(altered, ZipArchiveMode.Create))
        {
            if (mutation != "missing") Write(archive, "a.txt", mutation == "content" ? "changed"u8.ToArray() : "safe"u8.ToArray());
            Write(archive, "manifest.json", mutation == "manifest" ? "{}"u8.ToArray() : fixture.Manifest);
            if (mutation == "extra") Write(archive, "extra.txt", []);
            if (mutation == "duplicate-manifest") Write(archive, "manifest.json", fixture.Manifest);
        }
        Assert.False(await new PackageIntegrityVerifier().VerifyAsync(altered, fixture.Entries, fixture.Manifest, new PackageBuildLimits(), default));
    }

    [Fact]
    public async Task PublishRaceIsSafeAndTemporaryIsRemoved()
    {
        using var folder = new TestFolder();
        var destination = folder.PathOf("package.zip");
        var fileSystem = new FaultFileSystem(destination, failWrite: false, raceAtPublish: true);
        var builder = new PackageBuilder(new ContentRedactor(), new RedactionValidator(), new PackageIntegrityVerifier(), fileSystem);
        var result = await builder.BuildAsync(PackageBuilderTests.Request(destination, PackageBuilderTests.Entry("a.txt", "safe")));
        Assert.Equal(PackageBuildStatus.DestinationExists, result.Status);
        Assert.Equal("existing", await File.ReadAllTextAsync(destination));
        Assert.Empty(folder.TemporaryFiles());
    }

    [Fact]
    public async Task WriteFailurePublishesNothingAndTemporaryIsRemoved()
    {
        using var folder = new TestFolder();
        var destination = folder.PathOf("package.zip");
        var fileSystem = new FaultFileSystem(destination, failWrite: true, raceAtPublish: false);
        var builder = new PackageBuilder(new ContentRedactor(), new RedactionValidator(), new PackageIntegrityVerifier(), fileSystem);
        var result = await builder.BuildAsync(PackageBuilderTests.Request(destination, PackageBuilderTests.Entry("a.txt", "safe")));
        Assert.Equal(PackageBuildStatus.IoFailure, result.Status);
        Assert.False(File.Exists(destination));
        Assert.Empty(folder.TemporaryFiles());
    }

    [Fact]
    public async Task CancellationDuringIntegrityVerificationPublishesNothingAndRemovesTemporary()
    {
        using var folder = new TestFolder();
        var destination = folder.PathOf("package.zip");
        var builder = new PackageBuilder(new ContentRedactor(), new RedactionValidator(),
            new CancellingVerifier(), new AtomicPackageFileSystem());
        var result = await builder.BuildAsync(PackageBuilderTests.Request(destination,
            PackageBuilderTests.Entry("a.txt", "safe")));
        Assert.Equal(PackageBuildStatus.Cancelled, result.Status);
        Assert.False(File.Exists(destination));
        Assert.Empty(folder.TemporaryFiles());
    }

    [Fact]
    public async Task VerificationIoFailurePublishesNothingAndRemovesTemporary()
    {
        using var folder = new TestFolder();
        var destination = folder.PathOf("package.zip");
        var builder = new PackageBuilder(new ContentRedactor(), new RedactionValidator(),
            new ThrowingVerifier(), new AtomicPackageFileSystem());
        var result = await builder.BuildAsync(PackageBuilderTests.Request(destination,
            PackageBuilderTests.Entry("a.txt", "safe")));
        Assert.Equal(PackageBuildStatus.IoFailure, result.Status);
        Assert.False(File.Exists(destination));
        Assert.Empty(folder.TemporaryFiles());
    }

    [Fact]
    public async Task SuccessfulPublicationLeavesOneImmediatelyOpenableFinalFileAndNoTemporary()
    {
        using var folder = new TestFolder();
        var destination = folder.PathOf("package.zip");
        var result = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(destination,
            PackageBuilderTests.Entry("a.txt", "safe")));
        Assert.True(result.IsSuccess);
        Assert.Equal([destination], Directory.GetFiles(folder.Root));
        Assert.Empty(folder.TemporaryFiles());
        using var archive = ZipFile.Open(destination, ZipArchiveMode.Read, Encoding.UTF8);
        Assert.Equal(2, archive.Entries.Count);
    }

    [Fact]
    public async Task ReplacementImmediatelyBeforeMoveCannotPublishUnverifiedBytes()
    {
        using var folder = new TestFolder();
        var destination = folder.PathOf("package.zip");
        var fileSystem = new ReplacingPublishFileSystem();
        var builder = new PackageBuilder(new ContentRedactor(), new RedactionValidator(),
            new PackageIntegrityVerifier(), fileSystem);
        var result = await builder.BuildAsync(PackageBuilderTests.Request(destination,
            PackageBuilderTests.Entry("a.txt", "safe")));
        Assert.Equal(PackageBuildStatus.IntegrityFailure, result.Status);
        Assert.False(File.Exists(destination));
        Assert.Empty(folder.TemporaryFiles());
    }

    [Fact]
    public async Task CancellationImmediatelyAfterSuccessfulMoveReportsSuccess()
    {
        using var folder = new TestFolder();
        using var cancellation = new CancellationTokenSource();
        var destination = folder.PathOf("package.zip");
        var fileSystem = new CancelAfterMoveFileSystem(cancellation);
        var builder = new PackageBuilder(new ContentRedactor(), new RedactionValidator(),
            new PackageIntegrityVerifier(), fileSystem);
        var result = await builder.BuildAsync(PackageBuilderTests.Request(destination,
            PackageBuilderTests.Entry("a.txt", "safe")), cancellation.Token);
        Assert.True(result.IsSuccess);
        Assert.True(File.Exists(destination));
        Assert.Equal(PackageBuilder.Hash(await File.ReadAllBytesAsync(destination)), result.PackageSha256);
    }

    [Fact]
    public async Task CleanupExceptionDoesNotHidePrimaryWriteFailure()
    {
        using var folder = new TestFolder();
        var destination = folder.PathOf("package.zip");
        var fileSystem = new ThrowingCleanupFileSystem();
        var builder = new PackageBuilder(new ContentRedactor(), new RedactionValidator(),
            new PackageIntegrityVerifier(), fileSystem);
        var result = await builder.BuildAsync(PackageBuilderTests.Request(destination,
            PackageBuilderTests.Entry("a.txt", "safe")));
        Assert.Equal(PackageBuildStatus.IoFailure, result.Status);
        Assert.False(File.Exists(destination));
    }

    [Fact]
    public async Task ExistingTemporaryCandidateIsNeverDeletedAsIfOwned()
    {
        using var folder = new TestFolder();
        var foreignTemporary = folder.PathOf(".kronxy-package-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.tmp");
        await File.WriteAllTextAsync(foreignTemporary, "foreign-marker");
        var fileSystem = new ExistingTemporaryFileSystem(foreignTemporary);
        var builder = new PackageBuilder(new ContentRedactor(), new RedactionValidator(),
            new PackageIntegrityVerifier(), fileSystem);
        var result = await builder.BuildAsync(PackageBuilderTests.Request(folder.PathOf("package.zip"),
            PackageBuilderTests.Entry("a.txt", "safe")));
        Assert.Equal(PackageBuildStatus.IoFailure, result.Status);
        Assert.Equal("foreign-marker", await File.ReadAllTextAsync(foreignTemporary));
        Assert.False(File.Exists(folder.PathOf("package.zip")));
    }

    [Fact]
    public async Task TemporaryCandidateOutsideDestinationDirectoryIsRejected()
    {
        using var folder = new TestFolder();
        var otherDirectory = folder.PathOf("other");
        Directory.CreateDirectory(otherDirectory);
        var outsideTemporary = Path.Combine(otherDirectory,
            ".kronxy-package-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.tmp");
        var fileSystem = new ExistingTemporaryFileSystem(outsideTemporary);
        var builder = new PackageBuilder(new ContentRedactor(), new RedactionValidator(),
            new PackageIntegrityVerifier(), fileSystem);
        var result = await builder.BuildAsync(PackageBuilderTests.Request(folder.PathOf("package.zip"),
            PackageBuilderTests.Entry("a.txt", "safe")));
        Assert.Equal(PackageBuildStatus.IoFailure, result.Status);
        Assert.False(File.Exists(outsideTemporary));
        Assert.False(File.Exists(folder.PathOf("package.zip")));
    }

    [Theory]
    [InlineData("create")]
    [InlineData("close")]
    [InlineData("hash")]
    [InlineData("publish")]
    public async Task AtomicPhaseIoFailuresPublishNothingAndRemoveOwnedTemporary(string phase)
    {
        using var folder = new TestFolder();
        var destination = folder.PathOf("package.zip");
        IAtomicPackageFileSystem fileSystem = phase switch
        {
            "create" => new CreateFailureFileSystem(),
            "close" => new CloseFailureFileSystem(),
            "hash" => new HashFailureFileSystem(),
            _ => new PublishFailureFileSystem()
        };
        IPackageIntegrityVerifier verifier = phase == "hash" ? new AlwaysValidVerifier() : new PackageIntegrityVerifier();
        var builder = new PackageBuilder(new ContentRedactor(), new RedactionValidator(), verifier, fileSystem);
        var result = await builder.BuildAsync(PackageBuilderTests.Request(destination,
            PackageBuilderTests.Entry("a.txt", "safe")));
        Assert.Equal(PackageBuildStatus.IoFailure, result.Status);
        Assert.False(File.Exists(destination));
        Assert.Empty(folder.TemporaryFiles());
    }

    [Fact]
    public async Task CancellationDuringWritingAndImmediatelyBeforePublishLeavesNoPackage()
    {
        using var folder = new TestFolder();
        using var duringWrite = new CancellationTokenSource();
        using var beforePublish = new CancellationTokenSource();
        var writingBuilder = new PackageBuilder(new ContentRedactor(), new RedactionValidator(),
            new PackageIntegrityVerifier(), new CancelDuringWriteFileSystem(duringWrite));
        var beforePublishBuilder = new PackageBuilder(new ContentRedactor(), new RedactionValidator(),
            new PackageIntegrityVerifier(), new CancelOnDisposeFileSystem(beforePublish));

        var writing = await writingBuilder.BuildAsync(PackageBuilderTests.Request(folder.PathOf("writing.zip"),
            PackageBuilderTests.Entry("large.txt", new string('a', 20_000))), duringWrite.Token);
        var publishing = await beforePublishBuilder.BuildAsync(PackageBuilderTests.Request(folder.PathOf("publish.zip"),
            PackageBuilderTests.Entry("a.txt", "safe")), beforePublish.Token);

        Assert.Equal(PackageBuildStatus.Cancelled, writing.Status);
        Assert.Equal(PackageBuildStatus.Cancelled, publishing.Status);
        Assert.Empty(Directory.GetFiles(folder.Root, "*.zip"));
        Assert.Empty(folder.TemporaryFiles());
    }

    [Fact]
    public async Task UnexpectedPostPublicationVerificationFailureRemovesPublishedPackage()
    {
        using var folder = new TestFolder();
        var destination = folder.PathOf("package.zip");
        var builder = new PackageBuilder(new ContentRedactor(), new RedactionValidator(),
            new ThrowOnSecondVerification(), new AtomicPackageFileSystem());
        var result = await builder.BuildAsync(PackageBuilderTests.Request(destination,
            PackageBuilderTests.Entry("a.txt", "safe")));
        Assert.Equal(PackageBuildStatus.IntegrityFailure, result.Status);
        Assert.False(File.Exists(destination));
        Assert.Empty(folder.TemporaryFiles());
    }

    private static async Task<(PreparedPackageEntry[] Entries, byte[] Manifest)> FixtureAsync(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var content = Encoding.UTF8.GetBytes(await PackageBuilderTests.ReadAsync(archive.GetEntry("a.txt")!));
        await using var manifestStream = archive.GetEntry("manifest.json")!.Open();
        using var memory = new MemoryStream();
        await manifestStream.CopyToAsync(memory);
        return ([new PreparedPackageEntry("a.txt", PackageEntryKind.Source, content, PackageBuilder.Hash(content),
            [])], memory.ToArray());
    }

    private static void Write(ZipArchive archive, string name, byte[] bytes)
    { var entry = archive.CreateEntry(name, CompressionLevel.NoCompression); using var stream = entry.Open(); stream.Write(bytes); }

    private sealed class FaultFileSystem(string destination, bool failWrite, bool raceAtPublish) : IAtomicPackageFileSystem
    {
        private readonly AtomicPackageFileSystem inner = new();
        public bool Exists(string path) => inner.Exists(path);
        public bool ExistsSafely(string path) => inner.ExistsSafely(path);
        public string CreateTemporaryPath(string destinationPath) => inner.CreateTemporaryPath(destinationPath);
        public Stream CreateNew(string path) => failWrite ? new ThrowingStream(inner.CreateNew(path)) : inner.CreateNew(path);
        public Stream OpenRead(string path) => inner.OpenRead(path);
        public long Length(string path) => inner.Length(path);
        public PackagePublishStatus Publish(string temporaryPath, string destinationPath)
        {
            if (raceAtPublish) File.WriteAllText(destination, "existing");
            return inner.Publish(temporaryPath, destinationPath);
        }
        public void DeleteSafely(string path) => inner.DeleteSafely(path);
    }

    private sealed class ThrowingStream(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead; public override bool CanSeek => inner.CanSeek; public override bool CanWrite => true;
        public override long Length => inner.Length; public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush(); public override Task FlushAsync(CancellationToken token) => inner.FlushAsync(token);
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("synthetic write failure");
        public override void Write(ReadOnlySpan<byte> buffer) => throw new IOException("synthetic write failure");
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
        public override ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private sealed class CancellingVerifier : IPackageIntegrityVerifier
    {
        public Task<bool> VerifyAsync(Stream package, IReadOnlyList<PreparedPackageEntry> expectedEntries,
            byte[] expectedManifest, PackageBuildLimits limits, CancellationToken cancellationToken) =>
            throw new OperationCanceledException(cancellationToken);
    }

    private sealed class ThrowingVerifier : IPackageIntegrityVerifier
    {
        public Task<bool> VerifyAsync(Stream package, IReadOnlyList<PreparedPackageEntry> expectedEntries,
            byte[] expectedManifest, PackageBuildLimits limits, CancellationToken cancellationToken) =>
            throw new IOException("synthetic verifier failure");
    }

    private class DelegatingFileSystem : IAtomicPackageFileSystem
    {
        protected readonly AtomicPackageFileSystem Inner = new();
        public virtual bool Exists(string path) => Inner.Exists(path);
        public virtual bool ExistsSafely(string path) => Inner.ExistsSafely(path);
        public virtual string CreateTemporaryPath(string destinationPath) => Inner.CreateTemporaryPath(destinationPath);
        public virtual Stream CreateNew(string path) => Inner.CreateNew(path);
        public virtual Stream OpenRead(string path) => Inner.OpenRead(path);
        public virtual long Length(string path) => Inner.Length(path);
        public virtual PackagePublishStatus Publish(string temporaryPath, string destinationPath) =>
            Inner.Publish(temporaryPath, destinationPath);
        public virtual void DeleteSafely(string path) => Inner.DeleteSafely(path);
    }

    private sealed class ReplacingPublishFileSystem : DelegatingFileSystem
    {
        public override PackagePublishStatus Publish(string temporaryPath, string destinationPath)
        {
            File.AppendAllText(temporaryPath, string.Concat("synthetic", "-replacement"));
            return base.Publish(temporaryPath, destinationPath);
        }
    }

    private sealed class CancelAfterMoveFileSystem(CancellationTokenSource cancellation) : DelegatingFileSystem
    {
        public override PackagePublishStatus Publish(string temporaryPath, string destinationPath)
        {
            var result = base.Publish(temporaryPath, destinationPath);
            cancellation.Cancel();
            return result;
        }
    }

    private sealed class ThrowingCleanupFileSystem : DelegatingFileSystem
    {
        public override Stream CreateNew(string path) => new ThrowingStream(base.CreateNew(path));
        public override void DeleteSafely(string path) => throw new IOException("synthetic cleanup failure");
    }

    private sealed class ExistingTemporaryFileSystem(string foreignTemporary) : DelegatingFileSystem
    {
        public override string CreateTemporaryPath(string destinationPath) => foreignTemporary;
    }

    private sealed class CreateFailureFileSystem : DelegatingFileSystem
    {
        public override Stream CreateNew(string path) => throw new IOException("synthetic create failure");
    }

    private sealed class CloseFailureFileSystem : DelegatingFileSystem
    {
        public override Stream CreateNew(string path) => new CloseFailureStream(base.CreateNew(path));
    }

    private sealed class HashFailureFileSystem : DelegatingFileSystem
    {
        public override Stream CreateNew(string path) => new ReadFailureStream(base.CreateNew(path));
    }

    private sealed class PublishFailureFileSystem : DelegatingFileSystem
    {
        public override PackagePublishStatus Publish(string temporaryPath, string destinationPath) => PackagePublishStatus.IoFailure;
    }

    private sealed class CancelDuringWriteFileSystem(CancellationTokenSource cancellation) : DelegatingFileSystem
    {
        public override Stream CreateNew(string path) => new CancelDuringWriteStream(base.CreateNew(path), cancellation);
    }

    private sealed class CancelOnDisposeFileSystem(CancellationTokenSource cancellation) : DelegatingFileSystem
    {
        public override Stream CreateNew(string path) => new CancelOnDisposeStream(base.CreateNew(path), cancellation);
    }

    private sealed class AlwaysValidVerifier : IPackageIntegrityVerifier
    {
        public Task<bool> VerifyAsync(Stream package, IReadOnlyList<PreparedPackageEntry> expectedEntries,
            byte[] expectedManifest, PackageBuildLimits limits, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class ThrowOnSecondVerification : IPackageIntegrityVerifier
    {
        private int calls;
        public Task<bool> VerifyAsync(Stream package, IReadOnlyList<PreparedPackageEntry> expectedEntries,
            byte[] expectedManifest, PackageBuildLimits limits, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref calls) == 1) return Task.FromResult(true);
            throw new InvalidOperationException("synthetic post-publication verification failure");
        }
    }

    private abstract class DelegatingStream(Stream inner) : Stream
    {
        protected Stream Inner { get; } = inner;
        public override bool CanRead => Inner.CanRead;
        public override bool CanSeek => Inner.CanSeek;
        public override bool CanWrite => Inner.CanWrite;
        public override long Length => Inner.Length;
        public override long Position { get => Inner.Position; set => Inner.Position = value; }
        public override void Flush() => Inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => Inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => Inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => Inner.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => Inner.Seek(offset, origin);
        public override void SetLength(long value) => Inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => Inner.Write(buffer, offset, count);
        public override void Write(ReadOnlySpan<byte> buffer) => Inner.Write(buffer);
        protected override void Dispose(bool disposing) { if (disposing) Inner.Dispose(); base.Dispose(disposing); }
        public override ValueTask DisposeAsync() => Inner.DisposeAsync();
    }

    private sealed class CloseFailureStream(Stream inner) : DelegatingStream(inner)
    {
        public override async ValueTask DisposeAsync()
        {
            await Inner.DisposeAsync();
            throw new IOException("synthetic close failure");
        }
    }

    private sealed class ReadFailureStream(Stream inner) : DelegatingStream(inner)
    {
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("synthetic hash failure");
        public override int Read(Span<byte> buffer) => throw new IOException("synthetic hash failure");
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("synthetic hash failure"));
    }

    private sealed class CancelDuringWriteStream(Stream inner, CancellationTokenSource cancellation) : DelegatingStream(inner)
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            cancellation.Cancel();
            base.Write(buffer, offset, count);
        }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            cancellation.Cancel();
            base.Write(buffer);
        }
    }

    private sealed class CancelOnDisposeStream(Stream inner, CancellationTokenSource cancellation) : DelegatingStream(inner)
    {
        public override async ValueTask DisposeAsync()
        {
            await Inner.DisposeAsync();
            cancellation.Cancel();
        }
    }
}
