using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Context;
using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;
using Kronxy.Context.Configuration;
using Kronxy.Infrastructure.Context;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class ContextGenerationServiceTests :
    IDisposable
{
    private readonly string root;
    private readonly string workspace;
    private readonly string repositoryPath;
    private readonly Guid jobId;
    private readonly Guid runId;
    private readonly string head;
    private readonly RepositoryWorktreeHandle repository;

    public ContextGenerationServiceTests()
    {
        root =
            Path.Combine(
                Path.GetTempPath(),
                "kronxy-context-generation-tests",
                Guid.NewGuid().ToString("N"));

        workspace =
            Path.Combine(
                root,
                "workspace");

        repositoryPath =
            Path.Combine(
                workspace,
                "repo");

        Directory.CreateDirectory(
            repositoryPath);

        Directory.CreateDirectory(
            Path.Combine(
                repositoryPath,
                "src"));

        File.WriteAllText(
            Path.Combine(
                repositoryPath,
                "src",
                "Example.cs"),
            "namespace Sample; public sealed class Example { }");

        jobId =
            Guid.NewGuid();

        runId =
            Guid.NewGuid();

        head =
            new string('a', 40);

        repository =
            new RepositoryWorktreeHandle(
                jobId,
                "KRX-CONTEXT-TEST",
                workspace,
                repositoryPath,
                "kronxy/jobs/context-test",
                head,
                RepositoryWorktreeOperationKind.Created);
    }

    [Fact]
    public async Task Generates_valid_context_package_and_writes_artifact()
    {
        var executor =
            new InventoryExecutor(
                repository);

        var artifactStore =
            new RecordingArtifactStore();

        var service =
            new ContextGenerationService(
                executor,
                artifactStore,
                new ContextOptions());

        ContextGenerationResult result =
            await service.GenerateAsync(
                Request());

        Assert.True(
            result.IsSuccess);

        ContextPackageArtifact artifact =
            Assert.IsType<ContextPackageArtifact>(
                result.Artifact);

        Assert.Matches(
            "^[0-9a-f]{64}$",
            artifact.PackageId);

        Assert.Matches(
            "^[0-9a-f]{64}$",
            artifact.Sha256);

        Assert.Equal(
            1,
            artifact.EntryCount);

        Assert.Equal(
            "0.1.0",
            artifact.GeneratorVersion);

        ArtifactWriteRequest written =
            Assert.Single(
                artifactStore.Requests);

        Assert.Equal(
            jobId,
            written.JobId);

        Assert.Equal(
            runId,
            written.RunId);

        Assert.Equal(
            ArtifactType.ContextPackage,
            written.ArtifactType);

        byte[] bytes =
            written.Content.ToArray();

        Assert.Equal(
            artifact.SizeBytes,
            bytes.LongLength);

        using var memory =
            new MemoryStream(
                bytes,
                writable: false);

        using var archive =
            new ZipArchive(
                memory,
                ZipArchiveMode.Read);

        Assert.NotNull(
            archive.GetEntry(
                "src/Example.cs"));

        ZipArchiveEntry manifestEntry =
            Assert.IsType<ZipArchiveEntry>(
                archive.GetEntry(
                    "manifest.json"));

        using Stream manifestStream =
            manifestEntry.Open();

        using JsonDocument manifest =
            JsonDocument.Parse(
                manifestStream);

        Assert.Equal(
            artifact.PackageId,
            manifest.RootElement
                .GetProperty(
                    "packageId")
                .GetString());
    }

    [Fact]
    public async Task Rejects_repository_head_mismatch_before_git()
    {
        var executor =
            new InventoryExecutor(
                repository);

        var artifactStore =
            new RecordingArtifactStore();

        var service =
            new ContextGenerationService(
                executor,
                artifactStore,
                new ContextOptions());

        ContextGenerationResult result =
            await service.GenerateAsync(
                Request() with
                {
                    BaseRepositoryHead =
                        new string('b', 40)
                });

        Assert.False(
            result.IsSuccess);

        Assert.Equal(
            ContextGenerationFailureKind
                .RepositoryMismatch,
            result.FailureKind);

        Assert.Empty(
            executor.Requests);

        Assert.Empty(
            artifactStore.Requests);
    }

    [Fact]
    public async Task Invalid_job_binding_fails_before_git()
    {
        var executor =
            new InventoryExecutor(
                repository);

        var artifactStore =
            new RecordingArtifactStore();

        var service =
            new ContextGenerationService(
                executor,
                artifactStore,
                new ContextOptions());

        ContextGenerationResult result =
            await service.GenerateAsync(
                Request() with
                {
                    JobId =
                        Guid.NewGuid()
                });

        Assert.False(
            result.IsSuccess);

        Assert.Equal(
            ContextGenerationFailureKind
                .InvalidRequest,
            result.FailureKind);

        Assert.Empty(
            executor.Requests);
    }

    [Fact]
    public async Task Artifact_store_failure_is_fail_closed()
    {
        var executor =
            new InventoryExecutor(
                repository);

        var artifactStore =
            new RecordingArtifactStore
            {
                Fail = true
            };

        var service =
            new ContextGenerationService(
                executor,
                artifactStore,
                new ContextOptions());

        ContextGenerationResult result =
            await service.GenerateAsync(
                Request());

        Assert.False(
            result.IsSuccess);

        Assert.Equal(
            ContextGenerationFailureKind
                .ArtifactWriteFailure,
            result.FailureKind);
    }

    [Fact]
    public async Task Cancellation_is_propagated_as_typed_failure()
    {
        var executor =
            new InventoryExecutor(
                repository);

        var artifactStore =
            new RecordingArtifactStore();

        var service =
            new ContextGenerationService(
                executor,
                artifactStore,
                new ContextOptions());

        using var source =
            new CancellationTokenSource();

        source.Cancel();

        ContextGenerationResult result =
            await service.GenerateAsync(
                Request(),
                source.Token);

        Assert.False(
            result.IsSuccess);

        Assert.Equal(
            ContextGenerationFailureKind
                .Cancelled,
            result.FailureKind);
    }

    private ContextGenerationRequest Request() =>
        new()
        {
            JobId =
                jobId,

            RunId =
                runId,

            JobExternalId =
                repository.JobExternalId,

            Repository =
                repository,

            BaseRepositoryHead =
                head,

            CorrelationId =
                "context-generation-test"
        };

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    private sealed class InventoryExecutor :
        ISecureToolExecutor
    {
        private readonly RepositoryWorktreeHandle repository;

        public InventoryExecutor(
            RepositoryWorktreeHandle repository)
        {
            this.repository =
                repository;
        }

        public List<SecureToolRequest> Requests
        {
            get;
        } = [];

        public Task<SecureToolResult> ExecuteAsync(
            SecureToolRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            Requests.Add(
                request);

            string output =
                request.Operation switch
                {
                    SecureToolOperation.GitListIndex =>
                        "100644 " +
                        new string('b', 40) +
                        " 0\tsrc/Example.cs\0",

                    SecureToolOperation.GitListUntracked =>
                        string.Empty,

                    _ =>
                        throw new InvalidOperationException(
                            "Unexpected operation.")
                };

            DateTime now =
                DateTime.UtcNow;

            return Task.FromResult(
                new SecureToolResult(
                    ToolExecutionOutcome.Completed,
                    0,
                    output,
                    string.Empty,
                    new ToolExecutionAudit(
                        repository.JobId,
                        repository.JobExternalId,
                        request.Operation,
                        repository.WorkspacePath,
                        repository.RepositoryPath,
                        request.CorrelationId,
                        now,
                        now,
                        TimeSpan.Zero,
                        0,
                        ToolExecutionOutcome.Completed),
                    string.Empty));
        }
    }

    private sealed class RecordingArtifactStore :
        IArtifactStore
    {
        public List<ArtifactWriteRequest> Requests
        {
            get;
        } = [];

        public bool Fail
        {
            get;
            set;
        }

        public Task<ArtifactWriteResult> WriteAsync(
            ArtifactWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            Requests.Add(
                request);

            if (Fail)
            {
                return Task.FromResult(
                    ArtifactWriteResult.Failure(
                        ArtifactStoreFailureKind.IoFailure,
                        "ARTIFACT_TEST_FAILURE"));
            }

            byte[] content =
                request.Content.ToArray();

            string hash =
                Convert.ToHexString(
                        SHA256.HashData(
                            content))
                    .ToLowerInvariant();

            return Task.FromResult(
                ArtifactWriteResult.Success(
                    new ArtifactRecord
                    {
                        ArtifactId =
                            Guid.NewGuid(),

                        JobId =
                            request.JobId,

                        RunId =
                            request.RunId,

                        ArtifactType =
                            request.ArtifactType,

                        RelativePath =
                            $"{request.JobId:N}/" +
                            $"{request.RunId:N}/" +
                            "context/context.zip",

                        Sha256 =
                            hash,

                        SizeBytes =
                            content.LongLength,

                        CreatedAtUtc =
                            DateTimeOffset.UtcNow,

                        CorrelationId =
                            request.CorrelationId
                    }));
        }
    }
}
