using System.Diagnostics;
using Kronxy.Application.Execution;
using Kronxy.Infrastructure.Execution;
using Kronxy.Infrastructure.Git;
using Kronxy.Infrastructure.Workspaces;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class ExecutionPlaneLifecycleTests
{
    [Fact]
    public async Task New_manager_instances_recover_existing_session_and_executor_operates()
    {
        using var fixture = new Fixture();

        var jobId = Guid.NewGuid();

        var first =
            fixture.CreateLifecycle();

        var prepared =
            await first.PrepareAsync(
                jobId,
                "KRX-000401",
                fixture.Head());

        Assert.True(prepared.IsSuccess);
        Assert.NotNull(prepared.Session);

        var second =
            fixture.CreateLifecycle();

        var recovered =
            await second.RecoverAsync(
                jobId,
                "KRX-000401");

        Assert.True(recovered.IsSuccess);
        Assert.NotNull(recovered.Session);

        Assert.Equal(
            prepared.Session!.Workspace.Path,
            recovered.Session!.Workspace.Path);

        Assert.Equal(
            prepared.Session.Repository.RepositoryPath,
            recovered.Session.Repository.RepositoryPath);

        Assert.Equal(
            prepared.Session.Repository.Branch,
            recovered.Session.Repository.Branch);

        var executor =
            fixture.CreateExecutor();

        var result =
            await executor.ExecuteAsync(
                new SecureToolRequest
                {
                    Repository =
                        recovered.Session.Repository,
                    Operation =
                        SecureToolOperation.GitStatus,
                    CorrelationId =
                        "recovery-test"
                });

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Concurrent_prepare_for_same_job_converges_on_single_session()
    {
        using var fixture = new Fixture();

        var jobId = Guid.NewGuid();
        var head = fixture.Head();

        var operations =
            Enumerable.Range(0, 8)
                .Select(
                    _ =>
                        fixture.CreateLifecycle()
                            .PrepareAsync(
                                jobId,
                                "KRX-000402",
                                head))
                .ToArray();

        var results =
            await Task.WhenAll(
                operations);

        Assert.All(
            results,
            result =>
            {
                Assert.True(result.IsSuccess);
                Assert.NotNull(result.Session);
            });

        Assert.Single(
            results
                .Select(
                    result =>
                        result.Session!.Workspace.Path)
                .Distinct(
                    StringComparer.Ordinal));

        Assert.Single(
            results
                .Select(
                    result =>
                        result.Session!.Repository.RepositoryPath)
                .Distinct(
                    StringComparer.Ordinal));

        Assert.Single(
            results
                .Select(
                    result =>
                        result.Session!.Repository.Branch)
                .Distinct(
                    StringComparer.Ordinal));

        Assert.Equal(
            2,
            fixture.WorktreeCount());
    }

    [Fact]
    public async Task Cleanup_removes_git_worktree_before_workspace()
    {
        using var fixture = new Fixture();

        var jobId = Guid.NewGuid();

        var lifecycle =
            fixture.CreateLifecycle();

        var prepared =
            await lifecycle.PrepareAsync(
                jobId,
                "KRX-000403",
                fixture.Head());

        Assert.True(prepared.IsSuccess);
        Assert.NotNull(prepared.Session);

        var workspacePath =
            prepared.Session!.Workspace.Path;

        var repositoryPath =
            prepared.Session.Repository.RepositoryPath;

        Assert.Equal(
            2,
            fixture.WorktreeCount());

        var cleanup =
            await lifecycle.CleanupAsync(
                jobId,
                "KRX-000403");

        Assert.True(cleanup.IsSuccess);

        Assert.False(
            Directory.Exists(
                repositoryPath));

        Assert.False(
            Directory.Exists(
                workspacePath));

        Assert.Equal(
            1,
            fixture.WorktreeCount());
    }

    [Fact]
    public async Task Dirty_worktree_blocks_entire_cleanup_and_preserves_recovery_state()
    {
        using var fixture = new Fixture();

        var jobId = Guid.NewGuid();

        var lifecycle =
            fixture.CreateLifecycle();

        var prepared =
            await lifecycle.PrepareAsync(
                jobId,
                "KRX-000404",
                fixture.Head());

        Assert.True(prepared.IsSuccess);
        Assert.NotNull(prepared.Session);

        var repositoryPath =
            prepared.Session!.Repository.RepositoryPath;

        await File.WriteAllTextAsync(
            Path.Combine(
                repositoryPath,
                "untracked.txt"),
            "must survive");

        var cleanup =
            await lifecycle.CleanupAsync(
                jobId,
                "KRX-000404");

        Assert.False(cleanup.IsSuccess);

        Assert.Equal(
            ExecutionPlaneFailureKind.RepositoryFailure,
            cleanup.FailureKind);

        Assert.Equal(
            "REPOSITORY_WORKTREE_DIRTY",
            cleanup.ErrorCode);

        Assert.True(
            Directory.Exists(
                prepared.Session.Workspace.Path));

        Assert.True(
            Directory.Exists(
                repositoryPath));

        Assert.True(
            File.Exists(
                Path.Combine(
                    repositoryPath,
                    "untracked.txt")));

        Assert.Equal(
            2,
            fixture.WorktreeCount());

        var recovered =
            await fixture.CreateLifecycle()
                .RecoverAsync(
                    jobId,
                    "KRX-000404");

        Assert.True(recovered.IsSuccess);
    }

    [Fact]
    public async Task Completed_cleanup_can_be_prepared_again_without_collision()
    {
        using var fixture = new Fixture();

        var jobId = Guid.NewGuid();
        var head = fixture.Head();

        var lifecycle =
            fixture.CreateLifecycle();

        var first =
            await lifecycle.PrepareAsync(
                jobId,
                "KRX-000405",
                head);

        Assert.True(first.IsSuccess);
        Assert.NotNull(first.Session);

        var originalBranch =
            first.Session!.Repository.Branch;

        var cleanup =
            await lifecycle.CleanupAsync(
                jobId,
                "KRX-000405");

        Assert.True(cleanup.IsSuccess);
        Assert.Equal(
            1,
            fixture.WorktreeCount());

        var second =
            await fixture.CreateLifecycle()
                .PrepareAsync(
                    jobId,
                    "KRX-000405",
                    head);

        Assert.True(second.IsSuccess);
        Assert.NotNull(second.Session);

        Assert.Equal(
            originalBranch,
            second.Session!.Repository.Branch);

        Assert.Equal(
            2,
            fixture.WorktreeCount());
    }

    [Fact]
    public async Task Different_jobs_remain_isolated_end_to_end()
    {
        using var fixture = new Fixture();

        var lifecycle =
            fixture.CreateLifecycle();

        var first =
            await lifecycle.PrepareAsync(
                Guid.NewGuid(),
                "KRX-000406",
                fixture.Head());

        var second =
            await lifecycle.PrepareAsync(
                Guid.NewGuid(),
                "KRX-000407",
                fixture.Head());

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        Assert.NotNull(first.Session);
        Assert.NotNull(second.Session);

        Assert.NotEqual(
            first.Session!.Workspace.Path,
            second.Session!.Workspace.Path);

        Assert.NotEqual(
            first.Session.Repository.RepositoryPath,
            second.Session.Repository.RepositoryPath);

        Assert.NotEqual(
            first.Session.Repository.Branch,
            second.Session.Repository.Branch);

        Assert.Equal(
            3,
            fixture.WorktreeCount());
    }

    private sealed class Fixture :
        IDisposable
    {
        private readonly string container;

        public Fixture()
        {
            container =
                Path.Combine(
                    Path.GetTempPath(),
                    "kronxy-execution-plane-tests",
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

            File.WriteAllText(
                Path.Combine(
                    AuthoritativeRepository,
                    "Sample.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

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
        }

        public string AuthoritativeRepository { get; }

        public string WorkspaceRoot { get; }

        public ExecutionPlaneLifecycle
            CreateLifecycle()
        {
            var workspaceManager =
                new WorkspaceManager(
                    WorkspaceRoot);

            var repositoryManager =
                new RepositoryManager(
                    AuthoritativeRepository,
                    WorkspaceRoot,
                    TestToolResolver.Git());

            return new ExecutionPlaneLifecycle(
                workspaceManager,
                repositoryManager);
        }

        public SecureToolExecutor
            CreateExecutor()
        {
            return new SecureToolExecutor(
                SecureToolExecutorOptions.Create(
                    ResolveDotnet(),
                    TestToolResolver.Git()));
        }

        public string Head() =>
            Git(
                AuthoritativeRepository,
                "rev-parse",
                "HEAD").Trim();

        public int WorktreeCount()
        {
            var output =
                Git(
                    AuthoritativeRepository,
                    "worktree",
                    "list",
                    "--porcelain");

            return output
                .Split(
                    '\n',
                    StringSplitOptions.RemoveEmptyEntries)
                .Count(
                    line =>
                        line.StartsWith(
                            "worktree ",
                            StringComparison.Ordinal));
        }

        private static string ResolveDotnet()
        {
            var host =
                Environment.GetEnvironmentVariable(
                    "DOTNET_HOST_PATH");

            if (!string.IsNullOrWhiteSpace(host) &&
                Path.IsPathFullyQualified(host) &&
                File.Exists(host))
            {
                return host;
            }

            string known = TestToolResolver.Dotnet();

            if (!File.Exists(known))
            {
                throw new InvalidOperationException(
                    "dotnet executable not found.");
            }

            return known;
        }

        private static string Git(
            string workingDirectory,
            params string[] arguments)
        {
            var start =
                new ProcessStartInfo
                {
                    FileName = TestToolResolver.Git(),
                    WorkingDirectory =
                        workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

            start.Environment[
                "GIT_TERMINAL_PROMPT"] =
                "0";

            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(
                    argument);
            }

            using var process =
                Process.Start(start)
                ?? throw new InvalidOperationException(
                    "Unable to start temporary git.");

            var stdout =
                process.StandardOutput.ReadToEnd();

            var stderr =
                process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Temporary git failed: {stderr}");
            }

            return stdout;
        }

        public void Dispose()
        {
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
