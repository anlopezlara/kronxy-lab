using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kronxy.Application.Context;
using Kronxy.Infrastructure.Context;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class ContextAiInputBuilderTests
{
    [Fact]
    public async Task Valid_package_returns_deterministic_text()
    {
        byte[] package =
            CreatePackage(
                "src/a.cs",
                Encoding.UTF8.GetBytes("class A {}\n"));

        var builder = new ContextAiInputBuilder();

        ContextAiInputResult result =
            await builder.BuildAsync(
                new ContextAiInputRequest
                {
                    PackageContent = package,
                    MaxCharacters = 10_000
                });

        Assert.True(result.IsSuccess);
        Assert.Contains(
            "===== FILE: src/a.cs =====",
            result.Content);
        Assert.Contains(
            "class A {}",
            result.Content);
    }

    [Fact]
    public async Task Invalid_zip_fails_closed()
    {
        var builder = new ContextAiInputBuilder();

        ContextAiInputResult result =
            await builder.BuildAsync(
                new ContextAiInputRequest
                {
                    PackageContent =
                        Encoding.UTF8.GetBytes("not-a-zip"),
                    MaxCharacters = 10_000
                });

        Assert.False(result.IsSuccess);
        Assert.Equal(
            ContextAiInputFailureKind.InvalidPackage,
            result.FailureKind);
    }

    [Fact]
    public async Task Manifest_entry_mismatch_is_rejected()
    {
        byte[] package =
            CreatePackage(
                "src/a.cs",
                Encoding.UTF8.GetBytes("safe"),
                manifestPath: "src/b.cs");

        var builder = new ContextAiInputBuilder();

        ContextAiInputResult result =
            await builder.BuildAsync(
                new ContextAiInputRequest
                {
                    PackageContent = package,
                    MaxCharacters = 10_000
                });

        Assert.False(result.IsSuccess);
        Assert.Equal(
            ContextAiInputFailureKind.InvalidPackage,
            result.FailureKind);
    }

    [Fact]
    public async Task Hash_mismatch_is_rejected()
    {
        byte[] content =
            Encoding.UTF8.GetBytes("safe");

        byte[] package =
            CreatePackage(
                "src/a.cs",
                content,
                manifestHash:
                    new string('a', 64));

        var builder = new ContextAiInputBuilder();

        ContextAiInputResult result =
            await builder.BuildAsync(
                new ContextAiInputRequest
                {
                    PackageContent = package,
                    MaxCharacters = 10_000
                });

        Assert.False(result.IsSuccess);
        Assert.Equal(
            ContextAiInputFailureKind.InvalidPackage,
            result.FailureKind);
        Assert.Equal(
            "CONTEXT_AI_ENTRY_HASH_MISMATCH",
            result.ErrorCode);
    }

    [Fact]
    public async Task Invalid_utf8_is_rejected()
    {
        byte[] invalidUtf8 =
            [0xC3, 0x28];

        byte[] package =
            CreatePackage(
                "src/a.cs",
                invalidUtf8);

        var builder = new ContextAiInputBuilder();

        ContextAiInputResult result =
            await builder.BuildAsync(
                new ContextAiInputRequest
                {
                    PackageContent = package,
                    MaxCharacters = 10_000
                });

        Assert.False(result.IsSuccess);
        Assert.Equal(
            ContextAiInputFailureKind.InvalidEncoding,
            result.FailureKind);
    }

    [Fact]
    public async Task Character_limit_fails_closed()
    {
        byte[] package =
            CreatePackage(
                "src/a.cs",
                Encoding.UTF8.GetBytes(
                    new string('x', 100)));

        var builder = new ContextAiInputBuilder();

        ContextAiInputResult result =
            await builder.BuildAsync(
                new ContextAiInputRequest
                {
                    PackageContent = package,
                    MaxCharacters = 20
                });

        Assert.False(result.IsSuccess);
        Assert.Equal(
            ContextAiInputFailureKind.ContentTooLarge,
            result.FailureKind);
    }

    private static byte[] CreatePackage(
        string entryPath,
        byte[] content,
        string? manifestPath = null,
        string? manifestHash = null)
    {
        using var output = new MemoryStream();

        using (
            var archive =
                new ZipArchive(
                    output,
                    ZipArchiveMode.Create,
                    leaveOpen: true))
        {
            ZipArchiveEntry entry =
                archive.CreateEntry(
                    entryPath,
                    CompressionLevel.NoCompression);

            using (Stream stream = entry.Open())
            {
                stream.Write(content);
            }

            string hash =
                manifestHash ??
                Convert.ToHexString(
                    SHA256.HashData(content))
                .ToLowerInvariant();

            var manifest =
                new
                {
                    fileCount = 1,
                    entries = new[]
                    {
                        new
                        {
                            path =
                                manifestPath ??
                                entryPath,
                            kind = "Source",
                            sizeBytes =
                                content.LongLength,
                            sha256 = hash
                        }
                    }
                };

            byte[] manifestBytes =
                JsonSerializer.SerializeToUtf8Bytes(
                    manifest);

            ZipArchiveEntry manifestEntry =
                archive.CreateEntry(
                    "manifest.json",
                    CompressionLevel.NoCompression);

            using Stream manifestStream =
                manifestEntry.Open();

            manifestStream.Write(
                manifestBytes);
        }

        return output.ToArray();
    }
}
