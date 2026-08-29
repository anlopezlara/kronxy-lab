using System.IO.Compression;
using System.Text;
using Kronxy.Context.Packaging;
using Xunit;

namespace Kronxy.Context.Tests.Packaging;

public sealed class PackageCanonicalIntegrityTests
{
    [Fact]
    public async Task VerifierReturnsFalseForMissingPackagePath()
    {
        using var folder = new TestFolder();
        var fixture = Fixture();
        Assert.False(await new PackageIntegrityVerifier().VerifyAsync(folder.PathOf("missing.zip"),
            [fixture.Entry], PackageBuilder.SerializeManifest(fixture.Manifest), new PackageBuildLimits(), default));
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("generator")]
    [InlineData("id")]
    [InlineData("type")]
    [InlineData("stability")]
    [InlineData("timestamp")]
    [InlineData("count")]
    [InlineData("total")]
    [InlineData("path")]
    [InlineData("kind")]
    [InlineData("size")]
    [InlineData("hash")]
    [InlineData("redaction-category")]
    [InlineData("redaction-count")]
    public async Task VerifierRejectsSelfConsistentButInvalidManifestMetadata(string mutation)
    {
        using var folder = new TestFolder();
        var fixture = Fixture();
        var manifest = fixture.Manifest;
        manifest = mutation switch
        {
            "schema" => manifest with { SchemaVersion = string.Empty },
            "generator" => manifest with { GeneratorVersion = "9.9.9" },
            "id" => manifest with { PackageId = new string('0', 64) },
            "type" => manifest with { PackageType = "Unknown" },
            "stability" => manifest with { Stability = "Unknown" },
            "timestamp" => manifest with { GeneratedAtUtc = "2026-08-26" },
            "count" => manifest with { FileCount = 2 },
            "total" => manifest with { TotalContentBytes = 999 },
            "path" => manifest with { Entries = [manifest.Entries[0] with { Path = "../a.txt" }] },
            "kind" => manifest with { Entries = [manifest.Entries[0] with { Kind = "Unknown" }] },
            "size" => manifest with { Entries = [manifest.Entries[0] with { SizeBytes = 999 }] },
            "hash" => manifest with { Entries = [manifest.Entries[0] with { Sha256 = new string('A', 64) }] },
            "redaction-category" => manifest with { Entries = [manifest.Entries[0] with
                { Redactions = [new CanonicalRedactionSummary { Category = "Untrusted", Count = 1 }] }] },
            "redaction-count" => manifest with { Entries = [manifest.Entries[0] with
                { Redactions = [new CanonicalRedactionSummary { Category = "Password", Count = 0 }] }] },
            _ => throw new InvalidOperationException()
        };

        var manifestBytes = PackageBuilder.SerializeManifest(manifest);
        var package = folder.PathOf("altered.zip");
        WritePackage(package, fixture.Entry, manifestBytes);
        Assert.False(await new PackageIntegrityVerifier().VerifyAsync(package, [fixture.Entry], manifestBytes,
            new PackageBuildLimits(), default));
    }

    [Fact]
    public async Task VerifierRejectsNonCanonicalJsonEvenWhenItIsExpected()
    {
        using var folder = new TestFolder();
        var fixture = Fixture();
        var canonical = PackageBuilder.SerializeManifest(fixture.Manifest);
        var nonCanonical = canonical.Concat("\n"u8.ToArray()).ToArray();
        var package = folder.PathOf("package.zip");
        WritePackage(package, fixture.Entry, nonCanonical);
        Assert.False(await new PackageIntegrityVerifier().VerifyAsync(package, [fixture.Entry], nonCanonical,
            new PackageBuildLimits(), default));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("reordered")]
    [InlineData("null")]
    [InlineData("number-overflow")]
    public async Task VerifierRejectsNonCanonicalOrOutOfContractJsonShapes(string mutation)
    {
        using var folder = new TestFolder();
        var fixture = Fixture();
        var json = Encoding.UTF8.GetString(PackageBuilder.SerializeManifest(fixture.Manifest));
        json = mutation switch
        {
            "duplicate" => json.Replace("{\"schemaVersion\":\"1.0\"",
                "{\"schemaVersion\":\"1.0\",\"schemaVersion\":\"1.0\"", StringComparison.Ordinal),
            "unknown" => json.Insert(1, "\"unknown\":0,"),
            "reordered" => json.Replace("\"schemaVersion\":\"1.0\",\"generatorVersion\":\"0.1.0\"",
                "\"generatorVersion\":\"0.1.0\",\"schemaVersion\":\"1.0\"", StringComparison.Ordinal),
            "null" => json.Replace("\"packageId\":\"" + fixture.Manifest.PackageId + "\"",
                "\"packageId\":null", StringComparison.Ordinal),
            _ => json.Replace("\"fileCount\":1", "\"fileCount\":2147483648", StringComparison.Ordinal)
        };
        var bytes = Encoding.UTF8.GetBytes(json);
        var package = folder.PathOf("package.zip");
        WritePackage(package, fixture.Entry, bytes);
        Assert.False(await new PackageIntegrityVerifier().VerifyAsync(package, [fixture.Entry], bytes,
            new PackageBuildLimits(), default));
    }

    [Fact]
    public async Task VerifierRejectsExpectedHashThatDoesNotMatchContent()
    {
        using var folder = new TestFolder();
        var fixture = Fixture();
        var alteredEntry = fixture.Entry with { Sha256 = new string('0', 64) };
        var alteredManifest = fixture.Manifest with
        {
            PackageId = PackageBuilder.ComputePackageId(fixture.Manifest.SchemaVersion, fixture.Manifest.GeneratorVersion,
                fixture.Manifest.PackageType, fixture.Manifest.Stability, fixture.Manifest.GeneratedAtUtc, [alteredEntry]),
            Entries = [fixture.Manifest.Entries[0] with { Sha256 = alteredEntry.Sha256 }]
        };
        var manifestBytes = PackageBuilder.SerializeManifest(alteredManifest);
        var package = folder.PathOf("package.zip");
        WritePackage(package, alteredEntry, manifestBytes);
        Assert.False(await new PackageIntegrityVerifier().VerifyAsync(package, [alteredEntry], manifestBytes,
            new PackageBuildLimits(), default));
    }

    [Fact]
    public async Task VerifierRejectsUnsortedExpectedEntriesAndUnsafeExpectedPath()
    {
        using var folder = new TestFolder();
        var first = Entry("b.txt", "b");
        var second = Entry("a.txt", "a");
        var request = PackageBuilderTests.Request(folder.PathOf("unused.zip"));
        var manifest = PackageBuilder.CreateManifest(request, [first, second], 2);
        var bytes = PackageBuilder.SerializeManifest(manifest);
        var package = folder.PathOf("unsorted.zip");
        WritePackage(package, [first, second], bytes);
        Assert.False(await new PackageIntegrityVerifier().VerifyAsync(package, [first, second], bytes,
            new PackageBuildLimits(), default));

        var unsafeEntry = Entry("../a.txt", "a");
        var unsafeManifest = PackageBuilder.CreateManifest(request, [unsafeEntry], 1);
        var unsafeBytes = PackageBuilder.SerializeManifest(unsafeManifest);
        var unsafePackage = folder.PathOf("unsafe.zip");
        WritePackage(unsafePackage, unsafeEntry, unsafeBytes);
        Assert.False(await new PackageIntegrityVerifier().VerifyAsync(unsafePackage, [unsafeEntry], unsafeBytes,
            new PackageBuildLimits(), default));
    }

    [Fact]
    public async Task VerifierRejectsChangedZipEntryBytesAgainstCanonicalManifest()
    {
        using var folder = new TestFolder();
        var fixture = Fixture();
        var manifestBytes = PackageBuilder.SerializeManifest(fixture.Manifest);
        var package = folder.PathOf("package.zip");
        using (var archive = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            Write(archive, fixture.Entry.LogicalPath, "changed"u8.ToArray());
            Write(archive, PackagePath.ManifestName, manifestBytes);
        }
        Assert.False(await new PackageIntegrityVerifier().VerifyAsync(package, [fixture.Entry], manifestBytes,
            new PackageBuildLimits(), default));
    }

    [Fact]
    public async Task VerifierRejectsBytesAppendedAfterEndOfCentralDirectory()
    {
        using var folder = new TestFolder();
        var destination = folder.PathOf("valid.zip");
        Assert.True((await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(destination,
            PackageBuilderTests.Entry("a.txt", "safe")))).IsSuccess);
        var fixture = Fixture();
        await File.AppendAllTextAsync(destination, string.Concat("synthetic", "-trailing-material"));
        Assert.False(await new PackageIntegrityVerifier().VerifyAsync(destination, [fixture.Entry],
            PackageBuilder.SerializeManifest(fixture.Manifest), new PackageBuildLimits(), default));
    }

    [Theory]
    [InlineData("order")]
    [InlineData("compression")]
    [InlineData("timestamp")]
    [InlineData("attributes")]
    public async Task VerifierRejectsNonCanonicalZipStructureWithIdenticalContents(string mutation)
    {
        using var folder = new TestFolder();
        var fixture = Fixture();
        var manifestBytes = PackageBuilder.SerializeManifest(fixture.Manifest);
        var package = folder.PathOf("altered.zip");
        using (var archive = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            var ordered = mutation == "order"
                ? new[] { (PackagePath.ManifestName, manifestBytes), (fixture.Entry.LogicalPath, fixture.Entry.Content) }
                : new[] { (fixture.Entry.LogicalPath, fixture.Entry.Content), (PackagePath.ManifestName, manifestBytes) };
            foreach (var item in ordered)
            {
                var level = mutation == "compression" ? CompressionLevel.Optimal : CompressionLevel.NoCompression;
                var entry = archive.CreateEntry(item.Item1, level);
                entry.LastWriteTime = mutation == "timestamp"
                    ? PackageBuilderTests.FixedTime.AddMinutes(2)
                    : PackageBuilder.NormalizeZipTimestamp(PackageBuilderTests.FixedTime);
                entry.ExternalAttributes = mutation == "attributes" ? 32 : 0;
                using var stream = entry.Open();
                stream.Write(item.Item2);
            }
        }
        Assert.False(await new PackageIntegrityVerifier().VerifyAsync(package, [fixture.Entry], manifestBytes,
            new PackageBuildLimits(), default));
    }

    [Fact]
    public async Task VerifierIndependentlyEnforcesAggregateContentLimit()
    {
        using var folder = new TestFolder();
        var fixture = Fixture();
        var manifestBytes = PackageBuilder.SerializeManifest(fixture.Manifest);
        var package = folder.PathOf("package.zip");
        WritePackage(package, fixture.Entry, manifestBytes);
        var limits = new PackageBuildLimits { MaxTotalContentBytes = fixture.Entry.Content.Length - 1 };
        Assert.False(await new PackageIntegrityVerifier().VerifyAsync(package, [fixture.Entry], manifestBytes,
            limits, default));
    }

    private static (PreparedPackageEntry Entry, CanonicalPackageManifest Manifest) Fixture()
    {
        var entry = Entry("a.txt", "safe");
        var request = PackageBuilderTests.Request(Path.Combine(Path.GetTempPath(), "unused-kronxy-package.zip"));
        return (entry, PackageBuilder.CreateManifest(request, [entry], entry.Content.Length));
    }

    private static PreparedPackageEntry Entry(string path, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new PreparedPackageEntry(path, PackageEntryKind.Source, bytes, PackageBuilder.Hash(bytes), []);
    }

    private static void WritePackage(string path, PreparedPackageEntry entry, byte[] manifest) =>
        WritePackage(path, [entry], manifest);

    private static void WritePackage(string path, IReadOnlyList<PreparedPackageEntry> entries, byte[] manifest)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var entry in entries.OrderBy(item => item.LogicalPath, StringComparer.Ordinal))
            Write(archive, entry.LogicalPath, entry.Content);
        Write(archive, PackagePath.ManifestName, manifest);
    }

    private static void Write(ZipArchive archive, string name, byte[] content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
        using var stream = entry.Open();
        stream.Write(content);
    }
}
