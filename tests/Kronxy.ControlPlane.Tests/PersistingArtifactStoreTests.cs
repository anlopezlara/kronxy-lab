using Kronxy.Application.Artifacts;
using Kronxy.Infrastructure.Artifacts;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class PersistingArtifactStoreTests :
    IDisposable
{

    private readonly RecordingArtifactReader reader =
        new();


    private readonly string root =
        Path.Combine(
            Path.GetTempPath(),
            "kronxy-persisting-artifacts",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Successful_write_persists_metadata()
    {
        var repository =
            new RecordingMetadataRepository();

        var store =
            CreateStore(repository);

        ArtifactWriteResult result =
            await store.WriteAsync(
                Request());

        Assert.True(result.IsSuccess);

        ArtifactRecord artifact =
            Assert.IsType<ArtifactRecord>(
                result.Artifact);

        ArtifactRecord persisted =
            Assert.Single(
                repository.Added);

        Assert.Equal(
            artifact,
            persisted);
    }

    [Fact]
    public async Task Metadata_failure_is_fail_closed()
    {
        var repository =
            new RecordingMetadataRepository
            {
                Fail = true
            };

        var store =
            CreateStore(repository);

        ArtifactWriteResult result =
            await store.WriteAsync(
                Request());

        Assert.False(result.IsSuccess);

        Assert.Equal(
            ArtifactStoreFailureKind.IoFailure,
            result.FailureKind);

        Assert.Equal(
            "ARTIFACT_METADATA_PERSISTENCE_FAILED",
            result.ErrorCode);
    }

    [Fact]
    public async Task Filesystem_failure_does_not_persist_metadata()
    {
        var repository =
            new RecordingMetadataRepository();

        var store =
            CreateStore(repository);

        ArtifactWriteRequest request =
            Request();

        ArtifactWriteResult first =
            await store.WriteAsync(
                request);

        Assert.True(first.IsSuccess);

        ArtifactWriteResult second =
            await store.WriteAsync(
                request);

        Assert.False(second.IsSuccess);

        Assert.Equal(
            ArtifactStoreFailureKind.IntegrityFailure,
            second.FailureKind);

        Assert.Equal(
            "ARTIFACT_EXISTING_INTEGRITY_FAILED",
            second.ErrorCode);

        Assert.Single(
            repository.Added);

        Assert.Equal(
            1,
            reader.CallCount);
    }

    private PersistingArtifactStore CreateStore(
        RecordingMetadataRepository repository)
    {
        Directory.CreateDirectory(
            root);

        var options =
            new ArtifactStoreOptions
            {
                RootPath = root,
                MaxArtifactBytes =
                    1024 * 1024
            };

        var fileStore =
            new FileSystemArtifactStore(
                options);

        return new PersistingArtifactStore(
            fileStore,
            repository,
            reader);
    }

    private static ArtifactWriteRequest Request() =>
        new()
        {
            JobId =
                Guid.Parse(
                    "11111111-1111-1111-1111-111111111111"),

            RunId =
                Guid.Parse(
                    "22222222-2222-2222-2222-222222222222"),

            ArtifactType =
                ArtifactType.ContextPackage,

            Content =
                "context-package"u8.ToArray(),

            CorrelationId =
                "persisting-artifact-test"
        };

    [Fact]
    public async Task Exact_retry_reuses_existing_artifact()
    {
        var repository =
            new RecordingMetadataRepository();

        PersistingArtifactStore store =
            CreateStore(repository);

        ArtifactWriteRequest request =
            Request();

        ArtifactWriteResult first =
            await store.WriteAsync(request);

        Assert.True(first.IsSuccess);
        Assert.NotNull(first.Artifact);

        reader.Result =
            ArtifactReadResult.Success(
                first.Artifact!,
                request.Content.ToArray());

        ArtifactWriteResult retry =
            await store.WriteAsync(request);

        Assert.True(retry.IsSuccess);
        Assert.NotNull(retry.Artifact);

        Assert.Equal(
            first.Artifact!.ArtifactId,
            retry.Artifact!.ArtifactId);

        Assert.Single(
            repository.Added);

        Assert.Equal(
            1,
            reader.CallCount);
    }

    [Fact]
    public async Task Retry_with_different_content_fails_closed()
    {
        var repository =
            new RecordingMetadataRepository();

        PersistingArtifactStore store =
            CreateStore(repository);

        ArtifactWriteRequest firstRequest =
            Request();

        ArtifactWriteResult first =
            await store.WriteAsync(
                firstRequest);

        Assert.True(first.IsSuccess);

        ArtifactWriteRequest conflicting =
            firstRequest with
            {
                Content =
                    "different-content"u8.ToArray()
            };

        ArtifactWriteResult retry =
            await store.WriteAsync(
                conflicting);

        Assert.False(retry.IsSuccess);

        Assert.Equal(
            ArtifactStoreFailureKind.IntegrityFailure,
            retry.FailureKind);

        Assert.Equal(
            "ARTIFACT_EXISTING_CONTENT_CONFLICT",
            retry.ErrorCode);

        Assert.Equal(
            0,
            reader.CallCount);
    }

    [Fact]
    public async Task Physical_orphan_without_metadata_fails_closed()
    {
        var repository =
            new RecordingMetadataRepository
            {
                Fail = true
            };

        PersistingArtifactStore store =
            CreateStore(repository);

        ArtifactWriteRequest request =
            Request();

        ArtifactWriteResult first =
            await store.WriteAsync(request);

        Assert.False(first.IsSuccess);

        Assert.Equal(
            ArtifactStoreFailureKind.IoFailure,
            first.FailureKind);

        Assert.Empty(
            repository.Added);

        repository.Fail = false;

        ArtifactWriteResult retry =
            await store.WriteAsync(request);

        Assert.False(retry.IsSuccess);

        Assert.Equal(
            ArtifactStoreFailureKind.DestinationExists,
            retry.FailureKind);

        Assert.Equal(
            "ARTIFACT_EXISTING_METADATA_NOT_FOUND",
            retry.ErrorCode);

        Assert.Equal(
            0,
            reader.CallCount);
    }

    [Fact]
    public async Task Existing_metadata_with_invalid_file_fails_closed()
    {
        var repository =
            new RecordingMetadataRepository();

        PersistingArtifactStore store =
            CreateStore(repository);

        ArtifactWriteRequest request =
            Request();

        ArtifactWriteResult first =
            await store.WriteAsync(request);

        Assert.True(first.IsSuccess);
        Assert.NotNull(first.Artifact);

        reader.Result =
            ArtifactReadResult.Failure(
                ArtifactReadFailureKind.IntegrityFailure,
                "TEST_EXISTING_FILE_INVALID");

        ArtifactWriteResult retry =
            await store.WriteAsync(request);

        Assert.False(retry.IsSuccess);

        Assert.Equal(
            ArtifactStoreFailureKind.IntegrityFailure,
            retry.FailureKind);

        Assert.Equal(
            "ARTIFACT_EXISTING_INTEGRITY_FAILED",
            retry.ErrorCode);

        Assert.Equal(
            1,
            reader.CallCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    private sealed class RecordingArtifactReader :
        IArtifactReader
    {
        public int CallCount { get; private set; }

        public ArtifactReadResult Result { get; set; } =
            ArtifactReadResult.Failure(
                ArtifactReadFailureKind.NotFound,
                "TEST_ARTIFACT_NOT_FOUND");

        public Task<ArtifactReadResult> ReadAsync(
            ArtifactReadRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(Result);
        }
    }

    private sealed class RecordingMetadataRepository :
        IArtifactMetadataRepository
    {
        public bool Fail { get; set; }

        public List<ArtifactRecord> Added { get; } =
            [];

        public Task AddAsync(
            ArtifactRecord artifact,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (Fail)
            {
                throw new InvalidOperationException(
                    "TEST_METADATA_FAILURE");
            }

            Added.Add(
                artifact);

            return Task.CompletedTask;
        }

        public Task<ArtifactRecord?> GetByIdAsync(
            Guid artifactId,
            CancellationToken cancellationToken = default)
        {
            ArtifactRecord? artifact =
                Added.SingleOrDefault(
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
            IReadOnlyList<ArtifactRecord> artifacts =
                Added
                    .Where(
                        item =>
                            item.JobId == jobId &&
                            item.RunId == runId)
                    .ToArray();

            return Task.FromResult(
                artifacts);
        }
    }
}
