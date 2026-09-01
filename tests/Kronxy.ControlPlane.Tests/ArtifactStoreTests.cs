using System.Security.Cryptography;
using System.Text;
using Kronxy.Application.Artifacts;
using Kronxy.Infrastructure.Artifacts;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class ArtifactStoreTests
{
    [Fact]
    public async Task Write_creates_canonical_artifact_and_hash()
    {
        using var fixture =
            new ArtifactStoreFixture();

        var store =
            fixture.CreateStore();

        Guid jobId = Guid.NewGuid();
        Guid runId = Guid.NewGuid();

        byte[] content =
            Encoding.UTF8.GetBytes(
                "synthetic artifact");

        ArtifactWriteResult result =
            await store.WriteAsync(
                Request(
                    jobId,
                    runId,
                    ArtifactType.ContextPackage,
                    content));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Artifact);

        string expectedRelative =
            $"{jobId:N}/{runId:N}/context/context.zip";

        Assert.Equal(
            expectedRelative,
            result.Artifact!.RelativePath);

        Assert.False(
            Path.IsPathRooted(
                result.Artifact.RelativePath));

        Assert.DoesNotContain(
            "..",
            result.Artifact.RelativePath,
            StringComparison.Ordinal);

        string expectedHash =
            Convert.ToHexString(
                SHA256.HashData(content))
            .ToLowerInvariant();

        Assert.Equal(
            expectedHash,
            result.Artifact.Sha256);

        string physical =
            Path.Combine(
                fixture.Root,
                result.Artifact.RelativePath
                    .Replace(
                        '/',
                        Path.DirectorySeparatorChar));

        Assert.Equal(
            content,
            await File.ReadAllBytesAsync(
                physical));
    }

    [Fact]
    public async Task Existing_destination_is_never_overwritten()
    {
        using var fixture =
            new ArtifactStoreFixture();

        var store =
            fixture.CreateStore();

        Guid jobId = Guid.NewGuid();
        Guid runId = Guid.NewGuid();

        var first =
            await store.WriteAsync(
                Request(
                    jobId,
                    runId,
                    ArtifactType.BuildReport,
                    "first"u8.ToArray()));

        var second =
            await store.WriteAsync(
                Request(
                    jobId,
                    runId,
                    ArtifactType.BuildReport,
                    "second"u8.ToArray()));

        Assert.True(first.IsSuccess);

        Assert.Equal(
            ArtifactStoreFailureKind.DestinationExists,
            second.FailureKind);

        string physical =
            Path.Combine(
                fixture.Root,
                $"{jobId:N}",
                $"{runId:N}",
                "build",
                "report.json");

        Assert.Equal(
            "first",
            await File.ReadAllTextAsync(
                physical));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Empty_job_or_run_id_is_rejected(
        bool emptyJob,
        bool emptyRun)
    {
        using var fixture =
            new ArtifactStoreFixture();

        var store =
            fixture.CreateStore();

        var result =
            await store.WriteAsync(
                Request(
                    emptyJob
                        ? Guid.Empty
                        : Guid.NewGuid(),
                    emptyRun
                        ? Guid.Empty
                        : Guid.NewGuid(),
                    ArtifactType.GeneralReport,
                    []));

        Assert.Equal(
            ArtifactStoreFailureKind.InvalidRequest,
            result.FailureKind);
    }

    [Fact]
    public async Task Artifact_size_limit_fails_closed()
    {
        using var fixture =
            new ArtifactStoreFixture();

        var store =
            fixture.CreateStore(
                maxArtifactBytes: 3);

        var result =
            await store.WriteAsync(
                Request(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    ArtifactType.GeneralReport,
                    "1234"u8.ToArray()));

        Assert.Equal(
            ArtifactStoreFailureKind.ArtifactTooLarge,
            result.FailureKind);

        Assert.Empty(
            Directory.GetFiles(
                fixture.Root,
                "*",
                SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Cancellation_publishes_nothing()
    {
        using var fixture =
            new ArtifactStoreFixture();

        var store =
            fixture.CreateStore();

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        var result =
            await store.WriteAsync(
                Request(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    ArtifactType.ContextPackage,
                    "content"u8.ToArray()),
                cancellation.Token);

        Assert.Equal(
            ArtifactStoreFailureKind.Cancelled,
            result.FailureKind);

        Assert.Empty(
            Directory.GetFiles(
                fixture.Root,
                "*",
                SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Symlink_root_is_rejected()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture =
            new ArtifactStoreFixture(
                createRoot: false);

        string target =
            fixture.CreateSiblingDirectory(
                "target");

        Directory.CreateSymbolicLink(
            fixture.Root,
            target);

        var store =
            fixture.CreateStore();

        var result =
            await store.WriteAsync(
                Request(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    ArtifactType.ContextPackage,
                    "safe"u8.ToArray()));

        Assert.Equal(
            ArtifactStoreFailureKind.UnsafeRoot,
            result.FailureKind);

        Assert.Empty(
            Directory.GetFiles(
                target,
                "*",
                SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Symlink_intermediate_directory_is_rejected()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture =
            new ArtifactStoreFixture();

        Guid jobId = Guid.NewGuid();
        Guid runId = Guid.NewGuid();

        string jobPath =
            Path.Combine(
                fixture.Root,
                jobId.ToString("N"));

        Directory.CreateDirectory(
            jobPath);

        string outside =
            fixture.CreateSiblingDirectory(
                "outside");

        string runPath =
            Path.Combine(
                jobPath,
                runId.ToString("N"));

        Directory.CreateSymbolicLink(
            runPath,
            outside);

        var result =
            await fixture
                .CreateStore()
                .WriteAsync(
                    Request(
                        jobId,
                        runId,
                        ArtifactType.ContextPackage,
                        "safe"u8.ToArray()));

        Assert.Equal(
            ArtifactStoreFailureKind.UnsafePath,
            result.FailureKind);

        Assert.Empty(
            Directory.GetFiles(
                outside,
                "*",
                SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Symlink_destination_is_rejected()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture =
            new ArtifactStoreFixture();

        Guid jobId = Guid.NewGuid();
        Guid runId = Guid.NewGuid();

        string contextDirectory =
            Path.Combine(
                fixture.Root,
                jobId.ToString("N"),
                runId.ToString("N"),
                "context");

        Directory.CreateDirectory(
            contextDirectory);

        string outside =
            fixture.CreateSiblingFile(
                "outside.zip",
                "foreign");

        string destination =
            Path.Combine(
                contextDirectory,
                "context.zip");

        File.CreateSymbolicLink(
            destination,
            outside);

        var result =
            await fixture
                .CreateStore()
                .WriteAsync(
                    Request(
                        jobId,
                        runId,
                        ArtifactType.ContextPackage,
                        "safe"u8.ToArray()));

        Assert.Equal(
            ArtifactStoreFailureKind.UnsafePath,
            result.FailureKind);

        Assert.Equal(
            "foreign",
            await File.ReadAllTextAsync(
                outside));
    }

    [Fact]
    public void Options_reject_relative_root()
    {
        var options =
            new ArtifactStoreOptions
            {
                RootPath = "../artifacts",
                MaxArtifactBytes = 1024
            };

        Assert.Throws<InvalidOperationException>(
            options.Validate);
    }

    private static ArtifactWriteRequest Request(
        Guid jobId,
        Guid runId,
        ArtifactType type,
        byte[] content) =>
        new()
        {
            JobId = jobId,
            RunId = runId,
            ArtifactType = type,
            Content = content,
            CorrelationId = "test-correlation"
        };
}

internal sealed class ArtifactStoreFixture :
    IDisposable
{
    private readonly string parent;

    public ArtifactStoreFixture(
        bool createRoot = true)
    {
        parent =
            Path.Combine(
                Path.GetTempPath(),
                "kronxy-artifact-tests",
                Guid.NewGuid().ToString("N"));

        Root =
            Path.Combine(
                parent,
                "artifacts");

        Directory.CreateDirectory(
            parent);

        if (createRoot)
        {
            Directory.CreateDirectory(
                Root);
        }
    }

    public string Root { get; }

    public FileSystemArtifactStore CreateStore(
        long maxArtifactBytes = 1_048_576) =>
        new(
            new ArtifactStoreOptions
            {
                RootPath = Root,
                MaxArtifactBytes =
                    maxArtifactBytes
            });

    public string CreateSiblingDirectory(
        string name)
    {
        string path =
            Path.Combine(
                parent,
                name);

        Directory.CreateDirectory(path);

        return path;
    }

    public string CreateSiblingFile(
        string name,
        string content)
    {
        string path =
            Path.Combine(
                parent,
                name);

        File.WriteAllText(
            path,
            content);

        return path;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root) &&
                new DirectoryInfo(Root)
                    .LinkTarget is not null)
            {
                Directory.Delete(Root);
            }
        }
        catch
        {
        }

        try
        {
            if (Directory.Exists(parent))
            {
                Directory.Delete(
                    parent,
                    recursive: true);
            }
        }
        catch
        {
        }
    }
}
