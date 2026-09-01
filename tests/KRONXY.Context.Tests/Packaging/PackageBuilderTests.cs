using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kronxy.Context.Models;
using Kronxy.Context.Packaging;
using Xunit;

namespace Kronxy.Context.Tests.Packaging;

public sealed class PackageBuilderTests
{
    [Fact]
    public async Task BuildsCanonicalPackageWithRedactedContentAndCorrectHashes()
    {
        using var folder = new TestFolder();
        var secret = string.Concat("synthetic", "-credential");
        var result = await new PackageBuilder().BuildAsync(Request(folder.PathOf("package.zip"),
            Entry("src/config.env", "PASS" + "WORD=" + secret), Entry("docs/á.txt", "línea\r\n"), Entry("empty.txt", "")));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.EntryCount);
        var packageBytes = await File.ReadAllBytesAsync(folder.PathOf("package.zip"));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant(), result.PackageSha256);
        Assert.True(packageBytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(secret)) < 0);

        using var archive = ZipFile.OpenRead(folder.PathOf("package.zip"));
        Assert.Equal(["docs/á.txt", "empty.txt", "manifest.json", "src/config.env"], archive.Entries.Select(entry => entry.FullName));
        var content = await ReadAsync(archive.GetEntry("src/config.env")!);
        Assert.DoesNotContain(secret, content, StringComparison.Ordinal);
        Assert.Contains("__KRONXY_REDACTED_PASSWORD__", content, StringComparison.Ordinal);
        using var manifest = JsonDocument.Parse(await ReadAsync(archive.GetEntry("manifest.json")!));
        var root = manifest.RootElement;
        Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());
        Assert.Equal(3, root.GetProperty("fileCount").GetInt32());
        Assert.Equal(Encoding.UTF8.GetByteCount(content), root.GetProperty("entries")[2].GetProperty("sizeBytes").GetInt32());
        Assert.Equal(PackageBuilder.Hash(Encoding.UTF8.GetBytes(content)), root.GetProperty("entries")[2].GetProperty("sha256").GetString());
        Assert.DoesNotContain(secret, root.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SameRequestIsByteForByteDeterministicAcrossOrderCultureAndDestination()
    {
        using var folder = new TestFolder();
        var firstRequest = Request(folder.PathOf("one.zip"), Entry("b.txt", "dos"), Entry("a.txt", "uno"));
        var secondRequest = Request(folder.PathOf("two.zip"), Entry("a.txt", "uno"), Entry("b.txt", "dos"));
        var originalCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
            var first = await new PackageBuilder().BuildAsync(firstRequest);
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ja-JP");
            var second = await new PackageBuilder().BuildAsync(secondRequest);
            Assert.Equal(first.PackageSha256, second.PackageSha256);
            Assert.Equal(await File.ReadAllBytesAsync(firstRequest.DestinationPath), await File.ReadAllBytesAsync(secondRequest.DestinationPath));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = originalCulture; }
    }

    [Fact]
    public async Task ContentAndTimestampChangesAlterPackageHash()
    {
        using var folder = new TestFolder();
        var baseline = await new PackageBuilder().BuildAsync(Request(folder.PathOf("one.zip"), Entry("a.txt", "one")));
        var content = await new PackageBuilder().BuildAsync(Request(folder.PathOf("two.zip"), Entry("a.txt", "two")));
        var timestampRequest = Request(folder.PathOf("three.zip"), Entry("a.txt", "one")) with
        { GeneratedAtUtc = FixedTime.AddMinutes(2) };
        var timestamp = await new PackageBuilder().BuildAsync(timestampRequest);
        Assert.NotEqual(baseline.PackageSha256, content.PackageSha256);
        Assert.NotEqual(baseline.PackageSha256, timestamp.PackageSha256);
    }

    [Theory]
    [InlineData("../file.txt")]
    [InlineData("a/../../file.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("C:\\file.txt")]
    [InlineData("\\\\server\\share.txt")]
    [InlineData("//server/share.txt")]
    [InlineData("\\\\?\\C:\\file.txt")]
    [InlineData("\\\\.\\device")]
    [InlineData("file://host/file.txt")]
    [InlineData("a//b.txt")]
    [InlineData("./file.txt")]
    [InlineData("a/")]
    [InlineData("a\\b.txt")]
    [InlineData("manifest.json")]
    [InlineData("MANIFEST.JSON")]
    [InlineData("folder/CON.txt")]
    [InlineData("PRN")]
    [InlineData("folder/AUX.log")]
    [InlineData("folder/NUL.bin")]
    [InlineData("folder/COM1.txt")]
    [InlineData("folder/COM9")]
    [InlineData("folder/LPT1.txt")]
    [InlineData("folder/LPT9")]
    [InlineData("folder/file.txt:stream")]
    [InlineData("folder/a?.txt")]
    [InlineData("folder/trailing.")]
    [InlineData("folder/trailing ")]
    [InlineData("folder/ab\u202Ecd.txt")]
    public async Task UnsafeLogicalPathsFailBeforePublication(string path)
    {
        using var folder = new TestFolder();
        var destination = folder.PathOf("package.zip");
        var result = await new PackageBuilder().BuildAsync(Request(destination, Entry(path, "safe")));
        Assert.Equal(PackageBuildStatus.InvalidPath, result.Status);
        Assert.False(File.Exists(destination));
        Assert.Empty(folder.TemporaryFiles());
    }

    [Fact]
    public async Task UnicodeNormalizationCollisionIsRejected()
    {
        using var folder = new TestFolder();
        var composed = "caf\u00e9.txt";
        var decomposed = "cafe\u0301.txt";
        var result = await new PackageBuilder().BuildAsync(Request(folder.PathOf("package.zip"), Entry(composed, "a"), Entry(decomposed, "b")));
        Assert.Equal(PackageBuildStatus.DuplicateEntry, result.Status);
        Assert.False(File.Exists(folder.PathOf("package.zip")));
    }

    [Fact]
    public async Task NulAndControlCharactersFailBeforePublication()
    {
        using var folder = new TestFolder();
        foreach (var path in new[] { "a\0b.txt", "a\nb.txt" })
        {
            var result = await new PackageBuilder().BuildAsync(Request(folder.PathOf(Guid.NewGuid() + ".zip"), Entry(path, "safe")));
            Assert.Equal(PackageBuildStatus.InvalidPath, result.Status);
        }
        Assert.Empty(folder.TemporaryFiles());
    }

    [Theory]
    [InlineData("a.txt", "a.txt")]
    [InlineData("a.txt", "A.txt")]
    public async Task DuplicatePathsFailClosed(string first, string second)
    {
        using var folder = new TestFolder();
        var result = await new PackageBuilder().BuildAsync(Request(folder.PathOf("package.zip"), Entry(first, "a"), Entry(second, "b")));
        Assert.Equal(PackageBuildStatus.DuplicateEntry, result.Status);
        Assert.Empty(folder.TemporaryFiles());
    }

    [Fact]
    public async Task ExistingDestinationIsNeverOverwritten()
    {
        using var folder = new TestFolder();
        var destination = folder.PathOf("package.zip");
        await File.WriteAllTextAsync(destination, "existing");
        var result = await new PackageBuilder().BuildAsync(Request(destination, Entry("a.txt", "safe")));
        Assert.Equal(PackageBuildStatus.DestinationExists, result.Status);
        Assert.Equal("existing", await File.ReadAllTextAsync(destination));
    }

    [Fact]
    public async Task CancellationPublishesNothingAndLeavesNoTemporary()
    {
        using var folder = new TestFolder();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = await new PackageBuilder().BuildAsync(Request(folder.PathOf("package.zip"), Entry("a.txt", "safe")), cancellation.Token);
        Assert.Equal(PackageBuildStatus.Cancelled, result.Status);
        Assert.Empty(folder.TemporaryFiles());
        Assert.False(File.Exists(folder.PathOf("package.zip")));
    }

    [Fact]
    public async Task EmptyPackageIsAllowedAndImmediatelyReadable()
    {
        using var folder = new TestFolder();
        var destination = folder.PathOf("empty.zip");
        var result = await new PackageBuilder().BuildAsync(Request(destination));
        Assert.True(result.IsSuccess);
        using var archive = ZipFile.OpenRead(destination);
        Assert.Single(archive.Entries);
        Assert.Equal("manifest.json", archive.Entries[0].FullName);
    }

    [Fact]
    public async Task ZipDateBoundariesAreAcceptedAndOutsideOrOffsetDatesAreRejected()
    {
        using var folder = new TestFolder();
        foreach (var year in new[] { 1980, 2107 })
        {
            var request = Request(folder.PathOf($"valid-{year}.zip"), Entry("a.txt", "safe")) with
            { GeneratedAtUtc = new DateTimeOffset(year, 1, 1, 0, 0, 1, TimeSpan.Zero).AddTicks(1_234_567) };
            Assert.True((await new PackageBuilder().BuildAsync(request)).IsSuccess);
        }

        var before = await new PackageBuilder().BuildAsync(Request(folder.PathOf("before.zip")) with
        { GeneratedAtUtc = new DateTimeOffset(1979, 12, 31, 23, 59, 59, TimeSpan.Zero) });
        var after = await new PackageBuilder().BuildAsync(Request(folder.PathOf("after.zip")) with
        { GeneratedAtUtc = new DateTimeOffset(2108, 1, 1, 0, 0, 0, TimeSpan.Zero) });
        var offset = await new PackageBuilder().BuildAsync(Request(folder.PathOf("offset.zip")) with
        { GeneratedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(1)) });
        Assert.Equal(PackageBuildStatus.InvalidRequest, before.Status);
        Assert.Equal(PackageBuildStatus.InvalidRequest, after.Status);
        Assert.Equal(PackageBuildStatus.InvalidRequest, offset.Status);
    }

    internal static readonly DateTimeOffset FixedTime = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
    internal static PackageEntryRequest Entry(string path, string content) => new() { LogicalPath = path, Kind = PackageEntryKind.Source, Content = content };
    internal static PackageBuildRequest Request(string destination, params PackageEntryRequest[] entries) => new()
    {
        DestinationPath = destination, PackageType = PackageType.Handoff, Stability = PackageStability.Draft,
        GeneratedAtUtc = FixedTime, Entries = entries
    };
    internal static async Task<string> ReadAsync(ZipArchiveEntry entry)
    { await using var stream = entry.Open(); using var reader = new StreamReader(stream, Encoding.UTF8); return await reader.ReadToEndAsync(); }
}

internal sealed class TestFolder : IDisposable
{
    private readonly string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kronxy-packaging-tests", Guid.NewGuid().ToString("N"));
    public TestFolder() => Directory.CreateDirectory(path);
    public string Root => path;
    public string PathOf(string name) => System.IO.Path.Combine(path, name);
    public string[] TemporaryFiles() => Directory.GetFiles(path, ".kronxy-package-*.tmp");
    public void Dispose() { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
}
