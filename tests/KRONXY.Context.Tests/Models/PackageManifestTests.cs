using System.Text.Json;
using Kronxy.Context.Models;
using Xunit;

namespace Kronxy.Context.Tests.Models;

public sealed class PackageManifestTests
{
    [Fact]
    public void Defaults_InitializeContract()
    {
        var manifest = new PackageManifest();
        Assert.Equal("1.0", manifest.SchemaVersion);
        Assert.Equal("0.1.0", manifest.GeneratorVersion);
        Assert.Equal(".", manifest.RepositoryRoot);
        Assert.NotNull(manifest.IncludedFiles);
        Assert.NotNull(manifest.ExcludedFiles);
        Assert.NotNull(manifest.Redactions);
        Assert.NotNull(manifest.Commands);
        Assert.NotNull(manifest.ValidationResults);
        Assert.NotNull(manifest.Warnings);
    }

    [Fact]
    public void Serialize_UsesCamelCaseTextEnumsIsoDateAndEmptyArrays()
    {
        var manifest = new PackageManifest
        {
            PackageId = "pkg-1",
            PackageType = PackageType.Handoff,
            Stability = PackageStability.Draft,
            RepositoryName = "kronxy-lab",
            GeneratedAtUtc = new DateTimeOffset(2026, 8, 25, 12, 30, 0, TimeSpan.Zero),
            IncludedFiles = [new ManifestFileEntry { RelativePath = "src/file.cs" }]
        };

        var json = JsonSerializer.Serialize(manifest);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("pkg-1", root.GetProperty("packageId").GetString());
        Assert.Equal("Handoff", root.GetProperty("packageType").GetString());
        Assert.Equal("Draft", root.GetProperty("stability").GetString());
        Assert.Equal("2026-08-25T12:30:00+00:00", root.GetProperty("generatedAtUtc").GetString());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("includedFiles").ValueKind);
        Assert.Equal("src/file.cs", root.GetProperty("includedFiles")[0].GetProperty("relativePath").GetString());
    }

    [Fact]
    public void RepositoryRoot_DefaultDoesNotExposeAbsolutePath()
    {
        var json = JsonSerializer.Serialize(new PackageManifest());
        Assert.DoesNotContain(@"C:\", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"E:\", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RedactionRecord_CannotStoreSensitiveContent()
    {
        var names = typeof(RedactionRecord).GetProperties().Select(property => property.Name).ToArray();
        Assert.Equal([nameof(RedactionRecord.RelativePath), nameof(RedactionRecord.RuleId), nameof(RedactionRecord.Category),
            nameof(RedactionRecord.LineNumber), nameof(RedactionRecord.MatchCount)], names);
    }
}
