using Kronxy.Application.Workspaces;
using Kronxy.Infrastructure.Workspaces;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class WorkspaceManagerTests
{
    [Fact]
    public async Task Prepare_creates_deterministic_owned_workspace()
    {
        using var fixture = new WorkspaceFixture();
        var manager = fixture.CreateManager();
        var jobId = Guid.NewGuid();

        var result =
            await manager.PrepareAsync(
                jobId,
                "KRX-000123");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Workspace);
        Assert.Equal(
            WorkspaceOperationKind.Created,
            result.Workspace.Operation);
        Assert.True(
            Directory.Exists(
                result.Workspace.Path));

        Assert.StartsWith(
            Path.GetFullPath(fixture.Root),
            result.Workspace.Path,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Prepare_is_idempotent_for_same_job()
    {
        using var fixture = new WorkspaceFixture();
        var manager = fixture.CreateManager();
        var jobId = Guid.NewGuid();

        var first =
            await manager.PrepareAsync(
                jobId,
                "KRX-000124");

        var second =
            await manager.PrepareAsync(
                jobId,
                "KRX-000124");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        Assert.Equal(
            first.Workspace!.Path,
            second.Workspace!.Path);

        Assert.Equal(
            WorkspaceOperationKind.Existing,
            second.Workspace.Operation);
    }

    [Fact]
    public async Task Different_jobs_are_isolated()
    {
        using var fixture = new WorkspaceFixture();
        var manager = fixture.CreateManager();

        var first =
            await manager.PrepareAsync(
                Guid.NewGuid(),
                "KRX-000125");

        var second =
            await manager.PrepareAsync(
                Guid.NewGuid(),
                "KRX-000126");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        Assert.NotEqual(
            first.Workspace!.Path,
            second.Workspace!.Path);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("../../escape")]
    [InlineData("/tmp/escape")]
    [InlineData("KRX-../../escape")]
    [InlineData("KRX-00012;")]
    [InlineData("KRX-00012\n3")]
    public async Task Invalid_job_identity_is_rejected(
        string externalId)
    {
        using var fixture = new WorkspaceFixture();
        var manager = fixture.CreateManager();

        var result =
            await manager.PrepareAsync(
                Guid.NewGuid(),
                externalId);

        Assert.False(result.IsSuccess);

        Assert.Equal(
            WorkspaceFailureKind.InvalidJobIdentity,
            result.FailureKind);
    }

    [Fact]
    public async Task Symlink_workspace_root_is_rejected()
    {
        using var fixture = new WorkspaceFixture();

        var physicalRoot =
            Path.Combine(
                fixture.Container,
                "physical-root");

        var linkRoot =
            Path.Combine(
                fixture.Container,
                "link-root");

        Directory.CreateDirectory(physicalRoot);
        Directory.CreateSymbolicLink(
            linkRoot,
            physicalRoot);

        try
        {
            var manager =
                new WorkspaceManager(linkRoot);

            var result =
                await manager.PrepareAsync(
                    Guid.NewGuid(),
                    "KRX-000127");

            Assert.False(result.IsSuccess);

            Assert.Equal(
                WorkspaceFailureKind.UnsafeRoot,
                result.FailureKind);
        }
        finally
        {
            if (Directory.Exists(linkRoot))
            {
                Directory.Delete(linkRoot);
            }
        }
    }

    [Fact]
    public async Task Recovery_preserves_existing_owned_workspace()
    {
        using var fixture = new WorkspaceFixture();
        var firstManager = fixture.CreateManager();
        var jobId = Guid.NewGuid();

        var prepared =
            await firstManager.PrepareAsync(
                jobId,
                "KRX-000128");

        Assert.True(prepared.IsSuccess);

        var secondManager =
            fixture.CreateManager();

        var recovered =
            await secondManager.RecoverAsync(
                jobId,
                "KRX-000128");

        Assert.True(recovered.IsSuccess);

        Assert.Equal(
            WorkspaceOperationKind.Recovered,
            recovered.Workspace!.Operation);

        Assert.Equal(
            prepared.Workspace!.Path,
            recovered.Workspace.Path);
    }

    [Fact]
    public async Task Cleanup_is_idempotent_and_only_removes_owned_workspace()
    {
        using var fixture = new WorkspaceFixture();
        var manager = fixture.CreateManager();
        var jobId = Guid.NewGuid();

        var prepared =
            await manager.PrepareAsync(
                jobId,
                "KRX-000129");

        Assert.True(prepared.IsSuccess);

        var firstCleanup =
            await manager.CleanupAsync(
                jobId,
                "KRX-000129");

        Assert.True(firstCleanup.IsSuccess);

        Assert.Equal(
            WorkspaceOperationKind.Removed,
            firstCleanup.Workspace!.Operation);

        Assert.False(
            Directory.Exists(
                prepared.Workspace!.Path));

        var secondCleanup =
            await manager.CleanupAsync(
                jobId,
                "KRX-000129");

        Assert.True(secondCleanup.IsSuccess);

        Assert.Equal(
            WorkspaceOperationKind.AlreadyAbsent,
            secondCleanup.Workspace!.Operation);
    }

    [Fact]
    public async Task Cleanup_refuses_workspace_containing_symlink()
    {
        using var fixture = new WorkspaceFixture();
        var manager = fixture.CreateManager();
        var jobId = Guid.NewGuid();

        var prepared =
            await manager.PrepareAsync(
                jobId,
                "KRX-000130");

        Assert.True(prepared.IsSuccess);

        var outside =
            Path.Combine(
                fixture.Container,
                "outside");

        Directory.CreateDirectory(outside);

        var sentinel =
            Path.Combine(
                outside,
                "sentinel.txt");

        await File.WriteAllTextAsync(
            sentinel,
            "preserve");

        var link =
            Path.Combine(
                prepared.Workspace!.Path,
                "escape-link");

        Directory.CreateSymbolicLink(
            link,
            outside);

        var cleanup =
            await manager.CleanupAsync(
                jobId,
                "KRX-000130");

        Assert.False(cleanup.IsSuccess);

        Assert.Equal(
            WorkspaceFailureKind.UnsafePath,
            cleanup.FailureKind);

        Assert.True(File.Exists(sentinel));

        Directory.Delete(link);

        var safeCleanup =
            await manager.CleanupAsync(
                jobId,
                "KRX-000130");

        Assert.True(safeCleanup.IsSuccess);
    }

    [Fact]
    public async Task Concurrent_prepare_for_same_job_returns_one_workspace()
    {
        using var fixture = new WorkspaceFixture();
        var manager = fixture.CreateManager();
        var jobId = Guid.NewGuid();

        var tasks =
            Enumerable.Range(0, 8)
                .Select(_ =>
                    manager.PrepareAsync(
                        jobId,
                        "KRX-000131"))
                .ToArray();

        var results =
            await Task.WhenAll(tasks);

        Assert.All(
            results,
            result =>
                Assert.True(result.IsSuccess));

        Assert.Single(
            results
                .Select(result =>
                    result.Workspace!.Path)
                .Distinct(
                    StringComparer.Ordinal));
    }

    private sealed class WorkspaceFixture :
        IDisposable
    {
        public WorkspaceFixture()
        {
            Container =
                Path.Combine(
                    Path.GetTempPath(),
                    "kronxy-workspace-tests",
                    Guid.NewGuid().ToString("N"));

            Root =
                Path.Combine(
                    Container,
                    "workspaces");

            Directory.CreateDirectory(Container);
        }

        public string Container { get; }

        public string Root { get; }

        public WorkspaceManager CreateManager() =>
            new(Root);

        public void Dispose()
        {
            if (Directory.Exists(Container))
            {
                Directory.Delete(
                    Container,
                    recursive: true);
            }
        }
    }
}
