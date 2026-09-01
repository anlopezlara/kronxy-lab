using System.Diagnostics;
using Kronxy.Application.Repositories;
using Kronxy.Application.Workspaces;
using Kronxy.Infrastructure.Git;
using Kronxy.Infrastructure.Workspaces;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class RepositoryManagerTests
{
    [Fact]
    public async Task Prepare_creates_worktree_at_expected_head()
    {
        using var fixture = new RepositoryFixture();

        var workspace =
            await fixture.CreateWorkspaceAsync(
                "KRX-000201");

        var head =
            fixture.Git(
                fixture.AuthoritativeRepository,
                "rev-parse",
                "HEAD").Trim();

        var result =
            await fixture.Manager
                .PrepareWorktreeAsync(
                    workspace,
                    head);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        Assert.Equal(
            head,
            result.Value!.Head);

        Assert.StartsWith(
            "kronxy/jobs/krx-000201-",
            result.Value.Branch,
            StringComparison.Ordinal);

        Assert.True(
            Directory.Exists(
                result.Value.RepositoryPath));
    }

    [Fact]
    public async Task Prepare_is_idempotent_for_same_job()
    {
        using var fixture = new RepositoryFixture();

        var workspace =
            await fixture.CreateWorkspaceAsync(
                "KRX-000202");

        var head = fixture.Head();

        var first =
            await fixture.Manager
                .PrepareWorktreeAsync(
                    workspace,
                    head);

        var second =
            await fixture.Manager
                .PrepareWorktreeAsync(
                    workspace,
                    head);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        Assert.Equal(
            first.Value!.RepositoryPath,
            second.Value!.RepositoryPath);

        Assert.Equal(
            RepositoryWorktreeOperationKind.Existing,
            second.Value.Operation);
    }

    [Fact]
    public async Task Status_and_diff_are_reported()
    {
        using var fixture = new RepositoryFixture();

        var workspace =
            await fixture.CreateWorkspaceAsync(
                "KRX-000203");

        var prepared =
            await fixture.Manager
                .PrepareWorktreeAsync(
                    workspace,
                    fixture.Head());

        Assert.True(prepared.IsSuccess);
        Assert.NotNull(prepared.Value);

        var file =
            Path.Combine(
                prepared.Value!.RepositoryPath,
                "tracked.txt");

        await File.WriteAllTextAsync(
            file,
            "changed");

        var status =
            await fixture.Manager
                .GetStatusAsync(workspace);

        var diff =
            await fixture.Manager
                .GetDiffAsync(workspace);

        var stat =
            await fixture.Manager
                .GetDiffStatAsync(workspace);

        Assert.True(status.IsSuccess);
        Assert.False(status.Value!.IsClean);

        Assert.True(diff.IsSuccess);
        Assert.Contains(
            "tracked.txt",
            diff.Value!,
            StringComparison.Ordinal);

        Assert.True(stat.IsSuccess);
        Assert.Contains(
            "tracked.txt",
            stat.Value!,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Two_jobs_receive_independent_worktrees()
    {
        using var fixture = new RepositoryFixture();

        var firstWorkspace =
            await fixture.CreateWorkspaceAsync(
                "KRX-000204");

        var secondWorkspace =
            await fixture.CreateWorkspaceAsync(
                "KRX-000205");

        var first =
            await fixture.Manager
                .PrepareWorktreeAsync(
                    firstWorkspace,
                    fixture.Head());

        var second =
            await fixture.Manager
                .PrepareWorktreeAsync(
                    secondWorkspace,
                    fixture.Head());

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        Assert.NotNull(first.Value);
        Assert.NotNull(second.Value);

        Assert.NotEqual(
            first.Value!.RepositoryPath,
            second.Value!.RepositoryPath);

        Assert.NotEqual(
            first.Value.Branch,
            second.Value.Branch);
    }

    [Fact]
    public async Task Recovery_recognizes_existing_owned_worktree()
    {
        using var fixture = new RepositoryFixture();

        var workspace =
            await fixture.CreateWorkspaceAsync(
                "KRX-000206");

        var prepared =
            await fixture.Manager
                .PrepareWorktreeAsync(
                    workspace,
                    fixture.Head());

        Assert.True(prepared.IsSuccess);
        Assert.NotNull(prepared.Value);

        var recovered =
            await fixture.Manager
                .RecoverWorktreeAsync(
                    workspace);

        Assert.True(recovered.IsSuccess);
        Assert.NotNull(recovered.Value);

        Assert.Equal(
            RepositoryWorktreeOperationKind.Recovered,
            recovered.Value!.Operation);

        Assert.Equal(
            prepared.Value!.RepositoryPath,
            recovered.Value.RepositoryPath);
    }

    [Fact]
    public async Task Cleanup_removes_clean_worktree_but_preserves_workspace()
    {
        using var fixture = new RepositoryFixture();

        var workspace =
            await fixture.CreateWorkspaceAsync(
                "KRX-000207");

        var prepared =
            await fixture.Manager
                .PrepareWorktreeAsync(
                    workspace,
                    fixture.Head());

        Assert.True(prepared.IsSuccess);
        Assert.NotNull(prepared.Value);

        var cleanup =
            await fixture.Manager
                .CleanupWorktreeAsync(
                    workspace);

        Assert.True(cleanup.IsSuccess);
        Assert.NotNull(cleanup.Value);

        Assert.Equal(
            RepositoryWorktreeOperationKind.Removed,
            cleanup.Value!.Operation);

        Assert.False(
            Directory.Exists(
                prepared.Value!.RepositoryPath));

        Assert.True(
            Directory.Exists(
                workspace.Path));

        Assert.True(
            File.Exists(
                Path.Combine(
                    workspace.Path,
                    ".kronxy-workspace.json")));
    }

    [Fact]
    public async Task Cleanup_refuses_dirty_worktree()
    {
        using var fixture = new RepositoryFixture();

        var workspace =
            await fixture.CreateWorkspaceAsync(
                "KRX-000208");

        var prepared =
            await fixture.Manager
                .PrepareWorktreeAsync(
                    workspace,
                    fixture.Head());

        Assert.True(prepared.IsSuccess);
        Assert.NotNull(prepared.Value);

        await File.WriteAllTextAsync(
            Path.Combine(
                prepared.Value!.RepositoryPath,
                "untracked.txt"),
            "preserve");

        var cleanup =
            await fixture.Manager
                .CleanupWorktreeAsync(
                    workspace);

        Assert.False(cleanup.IsSuccess);

        Assert.Equal(
            RepositoryFailureKind.DirtyWorktree,
            cleanup.FailureKind);

        Assert.True(
            Directory.Exists(
                prepared.Value.RepositoryPath));
    }

    [Fact]
    public async Task Invalid_reference_is_rejected()
    {
        using var fixture = new RepositoryFixture();

        var workspace =
            await fixture.CreateWorkspaceAsync(
                "KRX-000209");

        var result =
            await fixture.Manager
                .PrepareWorktreeAsync(
                    workspace,
                    "--dangerous");

        Assert.False(result.IsSuccess);

        Assert.Equal(
            RepositoryFailureKind.InvalidReference,
            result.FailureKind);
    }

    [Fact]
    public async Task Foreign_repository_inside_workspace_is_rejected()
    {
        using var fixture = new RepositoryFixture();

        var workspace =
            await fixture.CreateWorkspaceAsync(
                "KRX-000210");

        var repositoryPath =
            Path.Combine(
                workspace.Path,
                "repository");

        Directory.CreateDirectory(
            repositoryPath);

        fixture.Git(
            repositoryPath,
            "init",
            "-b",
            "main");

        var result =
            await fixture.Manager
                .RecoverWorktreeAsync(
                    workspace);

        Assert.False(result.IsSuccess);
    }

    private sealed class RepositoryFixture :
        IDisposable
    {
        private readonly string container;

        public RepositoryFixture()
        {
            container =
                Path.Combine(
                    Path.GetTempPath(),
                    "kronxy-repository-tests",
                    Guid.NewGuid().ToString("N"));

            AuthoritativeRepository =
                Path.Combine(
                    container,
                    "authoritative");

            WorkspaceRoot =
                Path.Combine(
                    container,
                    "workspaces");

            Directory.CreateDirectory(
                AuthoritativeRepository);

            Directory.CreateDirectory(
                WorkspaceRoot);

            Git(
                AuthoritativeRepository,
                "init",
                "-b",
                "main");

            Git(
                AuthoritativeRepository,
                "config",
                "user.name",
                "KRONXY Tests");

            Git(
                AuthoritativeRepository,
                "config",
                "user.email",
                "kronxy@example.invalid");

            File.WriteAllText(
                Path.Combine(
                    AuthoritativeRepository,
                    "tracked.txt"),
                "initial");

            Git(
                AuthoritativeRepository,
                "add",
                "--all",
                "--");

            Git(
                AuthoritativeRepository,
                "commit",
                "-m",
                "initial");

            WorkspaceManager =
                new WorkspaceManager(
                    WorkspaceRoot);

            Manager =
                new RepositoryManager(
                    AuthoritativeRepository,
                    WorkspaceRoot,
                    TestToolResolver.Git());
        }

        public string AuthoritativeRepository { get; }

        public string WorkspaceRoot { get; }

        public WorkspaceManager WorkspaceManager { get; }

        public RepositoryManager Manager { get; }

        public string Head() =>
            Git(
                AuthoritativeRepository,
                "rev-parse",
                "HEAD").Trim();

        public async Task<WorkspaceHandle>
            CreateWorkspaceAsync(
                string externalId)
        {
            var result =
                await WorkspaceManager
                    .PrepareAsync(
                        Guid.NewGuid(),
                        externalId);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Workspace);

            return result.Workspace!;
        }

        public string Git(
            string workingDirectory,
            params string[] arguments)
        {
            var start =
                new ProcessStartInfo
                {
                    FileName = "git",
                    WorkingDirectory =
                        workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

            start.Environment["GIT_TERMINAL_PROMPT"] =
                "0";

            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            using var process =
                Process.Start(start)
                ?? throw new InvalidOperationException(
                    "Unable to start git.");

            var stdout =
                process.StandardOutput.ReadToEnd();

            var stderr =
                process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Temporary git repository setup failed: {stderr}");
            }

            return stdout;
        }

        public void Dispose()
        {
            try
            {
                Git(
                    AuthoritativeRepository,
                    "worktree",
                    "prune");
            }
            catch
            {
            }

            if (Directory.Exists(container))
            {
                foreach (var file in
                    Directory.EnumerateFiles(
                        container,
                        "*",
                        SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(
                            file,
                            FileAttributes.Normal);
                    }
                    catch
                    {
                    }
                }

                Directory.Delete(
                    container,
                    recursive: true);
            }
        }
    }
}
