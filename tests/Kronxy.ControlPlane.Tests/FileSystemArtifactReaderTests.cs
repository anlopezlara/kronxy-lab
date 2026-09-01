using System.Security.Cryptography;
using System.Text;
using Kronxy.Application.Artifacts;
using Kronxy.Infrastructure.Artifacts;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class FileSystemArtifactReaderTests :
    IDisposable
{
    private readonly string root;

    public FileSystemArtifactReaderTests()
    {
        root =
            Path.Combine(
                Path.GetTempPath(),
                "kronxy-reader-tests-" +
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);
    }

    [Fact]
    public async Task Read_valid_artifact_returns_verified_content()
    {
        Guid jobId = Guid.NewGuid();
        Guid runId = Guid.NewGuid();

        byte[] content =
            Encoding.UTF8.GetBytes(
                "verified-context-package");

        ArtifactRecord artifact =
            CreateArtifact(
                jobId,
                runId,
                ArtifactType.ContextPackage,
                content);

        WriteArtifact(
            artifact.RelativePath,
            content);

        var metadata =
            new FakeArtifactMetadataRepository(
                artifact);

        var reader =
            CreateReader(metadata);

        ArtifactReadResult result =
            await reader.ReadAsync(
                Request(
                    jobId,
                    runId,
                    ArtifactType.ContextPackage));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Artifact);
        Assert.Equal(
            artifact.ArtifactId,
            result.Artifact.ArtifactId);
        Assert.Equal(
            content,
            result.Content.ToArray());
    }

    [Fact]
    public async Task Read_hash_mismatch_fails_closed()
    {
        Guid jobId = Guid.NewGuid();
        Guid runId = Guid.NewGuid();

        byte[] expected =
            Encoding.UTF8.GetBytes(
                "expected-content");

        ArtifactRecord artifact =
            CreateArtifact(
                jobId,
                runId,
                ArtifactType.ContextPackage,
                expected);

        byte[] altered =
            Encoding.UTF8.GetBytes(
                "altered-content!");

        Assert.Equal(
            expected.Length,
            altered.Length);

        WriteArtifact(
            artifact.RelativePath,
            altered);

        var reader =
            CreateReader(
                new FakeArtifactMetadataRepository(
                    artifact));

        ArtifactReadResult result =
            await reader.ReadAsync(
                Request(
                    jobId,
                    runId,
                    ArtifactType.ContextPackage));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            ArtifactReadFailureKind.IntegrityFailure,
            result.FailureKind);
        Assert.Equal(
            "ARTIFACT_READ_HASH_MISMATCH",
            result.ErrorCode);
    }

    [Fact]
    public async Task Read_metadata_path_traversal_is_rejected()
    {
        Guid jobId = Guid.NewGuid();
        Guid runId = Guid.NewGuid();

        byte[] content =
            Encoding.UTF8.GetBytes(
                "context");

        ArtifactRecord artifact =
            CreateArtifact(
                jobId,
                runId,
                ArtifactType.ContextPackage,
                content) with
            {
                RelativePath =
                    "../outside/context.zip"
            };

        var reader =
            CreateReader(
                new FakeArtifactMetadataRepository(
                    artifact));

        ArtifactReadResult result =
            await reader.ReadAsync(
                Request(
                    jobId,
                    runId,
                    ArtifactType.ContextPackage));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            ArtifactReadFailureKind.UnsafePath,
            result.FailureKind);
        Assert.Equal(
            "ARTIFACT_READ_RELATIVE_PATH_UNSAFE",
            result.ErrorCode);
    }

    [Fact]
    public async Task Read_symlink_intermediate_directory_is_rejected()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Guid jobId = Guid.NewGuid();
        Guid runId = Guid.NewGuid();

        byte[] content =
            Encoding.UTF8.GetBytes(
                "context");

        ArtifactRecord artifact =
            CreateArtifact(
                jobId,
                runId,
                ArtifactType.ContextPackage,
                content);

        string outside =
            Path.Combine(
                Path.GetTempPath(),
                "kronxy-reader-outside-" +
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(outside);

        try
        {
            string jobPath =
                Path.Combine(
                    root,
                    jobId.ToString("N"));

            Directory.CreateDirectory(jobPath);

            string runPath =
                Path.Combine(
                    jobPath,
                    runId.ToString("N"));

            Directory.CreateDirectory(runPath);

            string outsideContext =
                Path.Combine(
                    outside,
                    "context");

            Directory.CreateDirectory(
                outsideContext);

            File.WriteAllBytes(
                Path.Combine(
                    outsideContext,
                    "context.zip"),
                content);

            string contextLink =
                Path.Combine(
                    runPath,
                    "context");

            Directory.CreateSymbolicLink(
                contextLink,
                outsideContext);

            var reader =
                CreateReader(
                    new FakeArtifactMetadataRepository(
                        artifact));

            ArtifactReadResult result =
                await reader.ReadAsync(
                    Request(
                        jobId,
                        runId,
                        ArtifactType.ContextPackage));

            Assert.False(result.IsSuccess);
            Assert.Equal(
                ArtifactReadFailureKind.SymlinkEscape,
                result.FailureKind);
        }
        finally
        {
            try
            {
                Directory.Delete(
                    outside,
                    recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task Read_request_limit_fails_closed()
    {
        Guid jobId = Guid.NewGuid();
        Guid runId = Guid.NewGuid();

        byte[] content =
            Encoding.UTF8.GetBytes(
                "0123456789");

        ArtifactRecord artifact =
            CreateArtifact(
                jobId,
                runId,
                ArtifactType.ContextPackage,
                content);

        WriteArtifact(
            artifact.RelativePath,
            content);

        var reader =
            CreateReader(
                new FakeArtifactMetadataRepository(
                    artifact));

        ArtifactReadResult result =
            await reader.ReadAsync(
                new ArtifactReadRequest
                {
                    JobId = jobId,
                    RunId = runId,
                    ArtifactType =
                        ArtifactType.ContextPackage,
                    MaxBytes = 5,
                    CorrelationId =
                        "reader-limit-test"
                });

        Assert.False(result.IsSuccess);
        Assert.Equal(
            ArtifactReadFailureKind.TooLarge,
            result.FailureKind);
        Assert.Equal(
            "ARTIFACT_READ_TOO_LARGE",
            result.ErrorCode);
    }

    private FileSystemArtifactReader CreateReader(
        IArtifactMetadataRepository metadata)
    {
        return new FileSystemArtifactReader(
            new ArtifactStoreOptions
            {
                RootPath = root,
                MaxArtifactBytes =
                    16_777_216
            },
            metadata);
    }

    private static ArtifactReadRequest Request(
        Guid jobId,
        Guid runId,
        ArtifactType artifactType)
    {
        return new ArtifactReadRequest
        {
            JobId = jobId,
            RunId = runId,
            ArtifactType = artifactType,
            MaxBytes = 16_777_216,
            CorrelationId =
                "reader-test"
        };
    }

    private ArtifactRecord CreateArtifact(
        Guid jobId,
        Guid runId,
        ArtifactType artifactType,
        byte[] content)
    {
        string directory =
            artifactType switch
            {
                ArtifactType.ContextPackage =>
                    "context",

                ArtifactType.AiResponse =>
                    "ai",

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(artifactType))
            };

        string fileName =
            artifactType switch
            {
                ArtifactType.ContextPackage =>
                    "context.zip",

                ArtifactType.AiResponse =>
                    "response.json",

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(artifactType))
            };

        string relative =
            $"{jobId:N}/" +
            $"{runId:N}/" +
            $"{directory}/" +
            fileName;

        string hash =
            Convert.ToHexString(
                SHA256.HashData(content))
            .ToLowerInvariant();

        return new ArtifactRecord
        {
            ArtifactId = Guid.NewGuid(),
            JobId = jobId,
            RunId = runId,
            ArtifactType = artifactType,
            RelativePath = relative,
            Sha256 = hash,
            SizeBytes = content.LongLength,
            CreatedAtUtc =
                DateTimeOffset.UtcNow,
            CorrelationId =
                "reader-test"
        };
    }

    private void WriteArtifact(
        string relativePath,
        byte[] content)
    {
        string path =
            Path.Combine(
                root,
                relativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));

        string? directory =
            Path.GetDirectoryName(path);

        Assert.NotNull(directory);

        Directory.CreateDirectory(
            directory);

        File.WriteAllBytes(
            path,
            content);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed class FakeArtifactMetadataRepository :
        IArtifactMetadataRepository
    {
        private readonly IReadOnlyList<ArtifactRecord>
            artifacts;

        public FakeArtifactMetadataRepository(
            params ArtifactRecord[] artifacts)
        {
            this.artifacts =
                artifacts;
        }

        public Task AddAsync(
            ArtifactRecord artifact,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ArtifactRecord?> GetByIdAsync(
            Guid artifactId,
            CancellationToken cancellationToken = default)
        {
            ArtifactRecord? artifact =
                artifacts.SingleOrDefault(
                    item =>
                        item.ArtifactId ==
                            artifactId);

            return Task.FromResult(
                artifact);
        }

        public Task<IReadOnlyList<ArtifactRecord>>
            GetByJobAndRunAsync(
                Guid jobId,
                Guid runId,
                CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ArtifactRecord> matching =
                artifacts
                    .Where(
                        item =>
                            item.JobId == jobId &&
                            item.RunId == runId)
                    .ToArray();

            return Task.FromResult(
                matching);
        }
    }
}
