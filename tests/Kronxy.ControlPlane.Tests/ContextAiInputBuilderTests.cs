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
    public async Task Prioritized_selection_is_deterministic_and_never_cuts_an_entry()
    {
        byte[] package = CreatePackage(
            [
                ("aaa-not-priority.txt", Encoding.UTF8.GetBytes(new string((char)120, 60))),
                ("src/priority.cs", Encoding.UTF8.GetBytes(new string((char)121, 60)))
            ]);
        var builder = new ContextAiInputBuilder();

        ContextAiInputResult first = await builder.BuildAsync(
            new ContextAiInputRequest { PackageContent = package, MaxCharacters = 100 });
        ContextAiInputResult second = await builder.BuildAsync(
            new ContextAiInputRequest { PackageContent = package, MaxCharacters = 100 });

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Content, second.Content);
        Assert.Contains("src/priority.cs", first.Content);
        Assert.Contains(new string((char)121, 60), first.Content);
        Assert.DoesNotContain("aaa-not-priority.txt", first.Content);
    }

    [Fact]
    public async Task Explicit_priority_paths_preserve_caller_order()
    {
        byte[] package =
            CreatePackage(
                [
                    (
                        "src/a.cs",
                        Encoding.UTF8.GetBytes(
                            "class A {}")),
                    (
                        "src/b.cs",
                        Encoding.UTF8.GetBytes(
                            "class B {}")),
                    (
                        "src/c.cs",
                        Encoding.UTF8.GetBytes(
                            "class C {}"))
                ]);

        var builder =
            new ContextAiInputBuilder();

        ContextAiInputResult result =
            await builder.BuildAsync(
                new ContextAiInputRequest
                {
                    PackageContent =
                        package,
                    MaxCharacters =
                        10_000,
                    PriorityPaths =
                    [
                        "src/c.cs",
                        "src/a.cs"
                    ]
                });

        Assert.True(
            result.IsSuccess);

        int c =
            result.Content.IndexOf(
                "===== FILE: src/c.cs =====",
                StringComparison.Ordinal);

        int a =
            result.Content.IndexOf(
                "===== FILE: src/a.cs =====",
                StringComparison.Ordinal);

        int b =
            result.Content.IndexOf(
                "===== FILE: src/b.cs =====",
                StringComparison.Ordinal);

        Assert.True(
            c >= 0);

        Assert.True(
            a > c);

        Assert.True(
            b > a);
    }

    [Fact]
    public async Task Unsafe_or_missing_priority_path_fails_closed()
    {
        byte[] package = CreatePackage(
            "src/a.cs",
            Encoding.UTF8.GetBytes("safe"));
        var builder = new ContextAiInputBuilder();

        ContextAiInputResult result = await builder.BuildAsync(
            new ContextAiInputRequest
            {
                PackageContent = package,
                MaxCharacters = 1_000,
                PriorityPaths = ["../escape.cs"]
            });

        Assert.False(result.IsSuccess);
        Assert.Equal("CONTEXT_AI_PRIORITY_PATH_INVALID", result.ErrorCode);
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
        IReadOnlyList<(string Path, byte[] Content)> files)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string path, byte[] content) in files)
            {
                ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.NoCompression);
                using Stream stream = entry.Open();
                stream.Write(content);
            }

            byte[] manifestBytes = JsonSerializer.SerializeToUtf8Bytes(new
            {
                fileCount = files.Count,
                entries = files.Select(file => new
                {
                    path = file.Path,
                    kind = "Source",
                    sizeBytes = file.Content.LongLength,
                    sha256 = Convert.ToHexString(SHA256.HashData(file.Content)).ToLowerInvariant()
                }).ToArray()
            });
            ZipArchiveEntry manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.NoCompression);
            using Stream manifestStream = manifestEntry.Open();
            manifestStream.Write(manifestBytes);
        }

        return output.ToArray();
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

    [Fact]
    public async Task Empty_allowed_path_prefixes_preserve_existing_behavior()
    {
        byte[] package =
            CreatePackage(
                [
                    (
                        "src/Kronxy.Domain/Projects/Project.cs",
                        Encoding.UTF8.GetBytes(
                            "class Project {}")),
                    (
                        "src/Kronxy.Api/Controllers/ProjectsController.cs",
                        Encoding.UTF8.GetBytes(
                            "class ProjectsController {}"))
                ]);

        var builder =
            new ContextAiInputBuilder();

        ContextAiInputResult result =
            await builder.BuildAsync(
                new ContextAiInputRequest
                {
                    PackageContent =
                        package,
                    MaxCharacters =
                        10_000,
                    AllowedPathPrefixes =
                        []
                });

        Assert.True(
            result.IsSuccess);

        Assert.Contains(
            "src/Kronxy.Domain/Projects/Project.cs",
            result.Content);

        Assert.Contains(
            "src/Kronxy.Api/Controllers/ProjectsController.cs",
            result.Content);
    }

    [Fact]
    public async Task Allowed_path_prefix_limits_context_content()
    {
        byte[] package =
            CreatePackage(
                [
                    (
                        "src/Kronxy.Domain/Projects/Project.cs",
                        Encoding.UTF8.GetBytes(
                            "class Project {}")),
                    (
                        "src/Kronxy.Domain/ProjectTasks/ProjectTask.cs",
                        Encoding.UTF8.GetBytes(
                            "class ProjectTask {}")),
                    (
                        "src/Kronxy.Api/Controllers/ProjectsController.cs",
                        Encoding.UTF8.GetBytes(
                            "class ProjectsController {}")),
                    (
                        "src/Kronxy.Application/Projects/GetProjectQuery.cs",
                        Encoding.UTF8.GetBytes(
                            "class GetProjectQuery {}")),
                    (
                        "src/Kronxy.Infrastructure/Repositories/ProjectRepository.cs",
                        Encoding.UTF8.GetBytes(
                            "class ProjectRepository {}"))
                ]);

        var builder =
            new ContextAiInputBuilder();

        ContextAiInputResult result =
            await builder.BuildAsync(
                new ContextAiInputRequest
                {
                    PackageContent =
                        package,
                    MaxCharacters =
                        20_000,
                    PriorityPaths =
                    [
                        "src/Kronxy.Domain/Projects/Project.cs"
                    ],
                    AllowedPathPrefixes =
                    [
                        "src/Kronxy.Domain/"
                    ]
                });

        Assert.True(
            result.IsSuccess);

        Assert.Contains(
            "src/Kronxy.Domain/Projects/Project.cs",
            result.Content);

        Assert.Contains(
            "src/Kronxy.Domain/ProjectTasks/ProjectTask.cs",
            result.Content);

        Assert.DoesNotContain(
            "src/Kronxy.Api/",
            result.Content);

        Assert.DoesNotContain(
            "src/Kronxy.Application/",
            result.Content);

        Assert.DoesNotContain(
            "src/Kronxy.Infrastructure/",
            result.Content);
    }

    [Fact]
    public async Task Unsafe_allowed_path_prefix_fails_closed()
    {
        byte[] package =
            CreatePackage(
                "src/a.cs",
                Encoding.UTF8.GetBytes(
                    "class A {}"));

        var builder =
            new ContextAiInputBuilder();

        ContextAiInputResult result =
            await builder.BuildAsync(
                new ContextAiInputRequest
                {
                    PackageContent =
                        package,
                    MaxCharacters =
                        10_000,
                    AllowedPathPrefixes =
                    [
                        "../src/"
                    ]
                });

        Assert.False(
            result.IsSuccess);

        Assert.Equal(
            ContextAiInputFailureKind.InvalidRequest,
            result.FailureKind);

        Assert.Equal(
            "CONTEXT_AI_ALLOWED_PATH_PREFIX_INVALID",
            result.ErrorCode);
    }

}
