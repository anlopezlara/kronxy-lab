using System.Security.Cryptography;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;
using Kronxy.Application.Workspaces;
using Kronxy.Infrastructure.Execution;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class ObservedChangeEvidenceServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "kronxy-observed", Guid.NewGuid().ToString("N"));
    private readonly string repositoryRoot;
    private readonly Guid jobId = Guid.NewGuid();
    private readonly Guid runId = Guid.NewGuid();

    public ObservedChangeEvidenceServiceTests()
    {
        repositoryRoot = Path.Combine(root, "repository");
        Directory.CreateDirectory(repositoryRoot);
    }

    [Fact]
    public async Task Correlates_change_and_persists_physical_hash_and_size()
    {
        await File.WriteAllTextAsync(Path.Combine(repositoryRoot, "new.txt"), "physical");
        var repository = new FakeRepository([new(ObservedRepositoryChangeKind.Created, "new.txt")]);
        var artifacts = new FakeArtifactStore();
        var result = await new ObservedChangeEvidenceService(repository, artifacts)
            .CaptureAsync(Request(DeveloperChangeOperationType.CreateFile, "new.txt"));

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(result.Manifest!.Entries);
        Assert.Equal(8, entry.FinalSizeBytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData("physical"u8)).ToLowerInvariant(), entry.FinalSha256);
        Assert.Equal(ArtifactType.ObservedChangeManifest, artifacts.Request!.ArtifactType);
        Assert.DoesNotContain(root, System.Text.Encoding.UTF8.GetString(artifacts.Request.Content.Span));
    }

    [Fact]
    public async Task Correction_observation_uses_separate_manifest_artifact()
    {
        await File.WriteAllTextAsync(
            Path.Combine(repositoryRoot, "new.txt"),
            "corrected");
        var repository = new FakeRepository(
            [new(ObservedRepositoryChangeKind.Created, "new.txt")]);
        var artifacts = new FakeArtifactStore();

        ObservedChangeEvidenceResult result =
            await new ObservedChangeEvidenceService(repository, artifacts)
                .CaptureAsync(
                    Request(
                        DeveloperChangeOperationType.CreateFile,
                        "new.txt") with
                    {
                        IsBuildCorrection = true
                    });

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ArtifactType.ObservedBuildCorrectionManifest,
            artifacts.Request!.ArtifactType);
    }

    [Fact]
    public async Task Resolves_observed_files_against_repository_path_not_workspace_path()
    {
        const string relativePath =
            "src/Kronxy.Domain/ProjectModules/ProjectModule.cs";
        string fullPath = Path.Combine(
            repositoryRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, "namespace Kronxy.Domain.ProjectModules;");

        var result = await new ObservedChangeEvidenceService(
            new FakeRepository([new(ObservedRepositoryChangeKind.Created, relativePath)]),
            new FakeArtifactStore())
            .CaptureAsync(Request(DeveloperChangeOperationType.CreateFile, relativePath));

        Assert.True(result.IsSuccess);
        Assert.Equal(relativePath, Assert.Single(result.Manifest!.Entries).RelativePath);
        Assert.False(File.Exists(Path.Combine(root, relativePath)));
    }

    [Theory]
    [InlineData(ObservedRepositoryChangeKind.Modified, "new.txt", "OBSERVED_CHANGE_KIND_MISMATCH")]
    [InlineData(ObservedRepositoryChangeKind.Created, "extra.txt", "OBSERVED_CHANGE_UNEXPECTED_PATH")]
    public async Task Fails_closed_for_wrong_kind_or_unexpected_path(
        ObservedRepositoryChangeKind kind, string path, string code)
    {
        var result = await new ObservedChangeEvidenceService(
            new FakeRepository([new(kind, path)]), new FakeArtifactStore())
            .CaptureAsync(Request(DeveloperChangeOperationType.CreateFile, "new.txt"));

        Assert.False(result.IsSuccess);
        Assert.Equal(code, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_symlink_before_hashing()
    {
        string target = Path.Combine(root, "outside.txt");
        await File.WriteAllTextAsync(target, "secret");
        File.CreateSymbolicLink(Path.Combine(repositoryRoot, "new.txt"), target);
        var result = await new ObservedChangeEvidenceService(
            new FakeRepository([new(ObservedRepositoryChangeKind.Created, "new.txt")]), new FakeArtifactStore())
            .CaptureAsync(Request(DeveloperChangeOperationType.CreateFile, "new.txt"));

        Assert.Equal(ObservedChangeEvidenceFailureKind.UnsafePath, result.FailureKind);
    }

    [Theory]
    [InlineData("../escape.cs")]
    [InlineData("folder/../../escape.cs")]
    public async Task Rejects_unsafe_relative_normalization(string path)
    {
        var result = await new ObservedChangeEvidenceService(
            new FakeRepository([new(ObservedRepositoryChangeKind.Created, path)]),
            new FakeArtifactStore())
            .CaptureAsync(Request(DeveloperChangeOperationType.CreateFile, path));

        Assert.Equal(ObservedChangeEvidenceFailureKind.UnsafePath, result.FailureKind);
        Assert.Equal("OBSERVED_CHANGE_FILE_UNSAFE", result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_absolute_path()
    {
        string path = Path.Combine(root, "outside-absolute.cs");
        await File.WriteAllTextAsync(path, "secret");

        var result = await new ObservedChangeEvidenceService(
            new FakeRepository([new(ObservedRepositoryChangeKind.Created, path)]),
            new FakeArtifactStore())
            .CaptureAsync(Request(DeveloperChangeOperationType.CreateFile, path));

        Assert.Equal(ObservedChangeEvidenceFailureKind.UnsafePath, result.FailureKind);
        Assert.Equal("OBSERVED_CHANGE_FILE_UNSAFE", result.ErrorCode);
    }

    private ObservedChangeEvidenceRequest Request(DeveloperChangeOperationType operation, string path) =>
        new()
        {
            JobId = jobId,
            RunId = runId,
            Repository = new(jobId, "KRX-1", root, repositoryRoot, "branch", new string('a', 40), RepositoryWorktreeOperationKind.Existing),
            Proposal = new("summary",
                [new(operation, path, "intent", "ignored", new string('b', 64), 7)],
                [], [], 7, 100)
        };

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    private sealed class FakeArtifactStore : IArtifactStore
    {
        public ArtifactWriteRequest? Request { get; private set; }
        public Task<ArtifactWriteResult> WriteAsync(ArtifactWriteRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(ArtifactWriteResult.Success(new ArtifactRecord
            {
                ArtifactId = Guid.NewGuid(), JobId = request.JobId, RunId = request.RunId,
                ArtifactType = request.ArtifactType, RelativePath = "changes/manifest.json",
                Sha256 = new string('c', 64), SizeBytes = request.Content.Length,
                CreatedAtUtc = DateTimeOffset.UtcNow, CorrelationId = request.CorrelationId
            }));
        }
    }

    private sealed class FakeRepository(IReadOnlyList<ObservedRepositoryChange> changes) : IRepositoryManager
    {
        public Task<RepositoryOperationResult<IReadOnlyList<ObservedRepositoryChange>>> GetObservedChangesAsync(WorkspaceHandle workspace, CancellationToken cancellationToken = default) =>
            Task.FromResult(RepositoryOperationResult<IReadOnlyList<ObservedRepositoryChange>>.Success(changes));
        public Task<RepositoryOperationResult<RepositoryWorktreeHandle>> PrepareWorktreeAsync(WorkspaceHandle w,string h,CancellationToken c=default)=>throw new NotSupportedException();
        public Task<RepositoryOperationResult<RepositoryWorktreeHandle>> RecoverWorktreeAsync(WorkspaceHandle w,CancellationToken c=default)=>throw new NotSupportedException();
        public Task<RepositoryOperationResult<string>> GetHeadAsync(WorkspaceHandle w,CancellationToken c=default)=>throw new NotSupportedException();
        public Task<RepositoryOperationResult<string>> GetCurrentBranchAsync(WorkspaceHandle w,CancellationToken c=default)=>throw new NotSupportedException();
        public Task<RepositoryOperationResult<RepositoryStatus>> GetStatusAsync(WorkspaceHandle w,CancellationToken c=default)=>throw new NotSupportedException();
        public Task<RepositoryOperationResult<string>> GetDiffAsync(WorkspaceHandle w,CancellationToken c=default)=>throw new NotSupportedException();
        public Task<RepositoryOperationResult<string>> GetDiffStatAsync(WorkspaceHandle w,CancellationToken c=default)=>throw new NotSupportedException();
        public Task<RepositoryOperationResult<RepositoryWorktreeHandle>> CleanupWorktreeAsync(WorkspaceHandle w,CancellationToken c=default)=>throw new NotSupportedException();
    }
}
