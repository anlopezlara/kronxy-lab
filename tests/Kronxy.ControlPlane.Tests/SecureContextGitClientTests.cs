using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;
using Kronxy.Context.Git;
using Kronxy.Infrastructure.Context;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class SecureContextGitClientTests
{
    [Fact]
    public async Task DiscoverRoot_returns_only_bound_repository()
    {
        var fixture =
            new Fixture();

        string root =
            await fixture.Client
                .DiscoverRootAsync(
                    fixture.Repository.RepositoryPath);

        Assert.Equal(
            Path.GetFullPath(
                fixture.Repository.RepositoryPath),
            root);
    }

    [Fact]
    public async Task DiscoverRoot_rejects_other_path()
    {
        var fixture =
            new Fixture();

        await Assert.ThrowsAsync<GitClientException>(
            () =>
                fixture.Client
                    .DiscoverRootAsync(
                        Path.GetTempPath()));
    }

    [Fact]
    public async Task RepositoryInfo_uses_pinned_head_without_git_execution()
    {
        var fixture =
            new Fixture();

        RepositoryInfo result =
            await fixture.Client
                .GetRepositoryInfoAsync(
                    fixture.Repository.RepositoryPath);

        Assert.Equal(
            fixture.Repository.Head,
            result.HeadCommit,
            ignoreCase: true);

        Assert.True(result.IsHeadDetached);

        Assert.Empty(
            fixture.Executor.Requests);
    }

    [Fact]
    public async Task ResolveCommit_accepts_only_head_or_pinned_head()
    {
        var fixture =
            new Fixture();

        string head =
            await fixture.Client
                .ResolveCommitAsync(
                    fixture.Repository.RepositoryPath,
                    "HEAD");

        Assert.Equal(
            fixture.Repository.Head,
            head,
            ignoreCase: true);

        await Assert.ThrowsAsync<GitClientException>(
            () =>
                fixture.Client.ResolveCommitAsync(
                    fixture.Repository.RepositoryPath,
                    "main"));
    }

    [Fact]
    public async Task Index_uses_only_secure_allowlisted_operation()
    {
        var fixture =
            new Fixture();

        fixture.Executor.NextResult =
            fixture.Success(
                "100644 " +
                new string('a', 40) +
                " 0\tsrc/file.cs\0",
                SecureToolOperation.GitListIndex);

        IReadOnlyList<GitIndexEntry> result =
            await fixture.Client
                .GetIndexEntriesAsync(
                    fixture.Repository.RepositoryPath);

        Assert.Single(result);

        SecureToolRequest request =
            Assert.Single(
                fixture.Executor.Requests);

        Assert.Equal(
            SecureToolOperation.GitListIndex,
            request.Operation);

        Assert.Null(request.Target);
    }

    [Fact]
    public async Task Untracked_uses_only_secure_allowlisted_operation()
    {
        var fixture =
            new Fixture();

        fixture.Executor.NextResult =
            fixture.Success(
                "one.cs\0two.cs\0",
                SecureToolOperation.GitListUntracked);

        IReadOnlyList<string> result =
            await fixture.Client
                .GetUntrackedFilesAsync(
                    fixture.Repository.RepositoryPath);

        Assert.Equal(
            ["one.cs", "two.cs"],
            result);

        SecureToolRequest request =
            Assert.Single(
                fixture.Executor.Requests);

        Assert.Equal(
            SecureToolOperation.GitListUntracked,
            request.Operation);

        Assert.Null(request.Target);
    }

    [Fact]
    public async Task Unsupported_git_operations_fail_closed()
    {
        var fixture =
            new Fixture();

        await Assert.ThrowsAsync<GitClientException>(
            () =>
                fixture.Client
                    .GetWorkingTreeStatusAsync(
                        fixture.Repository.RepositoryPath));

        await Assert.ThrowsAsync<GitClientException>(
            () =>
                fixture.Client
                    .GetChangesAsync(
                        fixture.Repository.RepositoryPath,
                        "HEAD~1",
                        "HEAD"));

        await Assert.ThrowsAsync<GitClientException>(
            () =>
                fixture.Client
                    .IsAncestorAsync(
                        fixture.Repository.RepositoryPath,
                        "HEAD~1",
                        "HEAD"));

        Assert.Empty(
            fixture.Executor.Requests);
    }

    [Fact]
    public async Task Secure_executor_failure_is_fail_closed()
    {
        var fixture =
            new Fixture();

        fixture.Executor.NextResult =
            fixture.Failure(
                SecureToolOperation.GitListIndex);

        await Assert.ThrowsAsync<GitClientException>(
            () =>
                fixture.Client
                    .GetIndexEntriesAsync(
                        fixture.Repository.RepositoryPath));
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            string workspace =
                Path.Combine(
                    Path.GetTempPath(),
                    "kronxy-context-git-tests",
                    Guid.NewGuid().ToString("N"));

            string repositoryPath =
                Path.Combine(
                    workspace,
                    "repo");

            Repository =
                new RepositoryWorktreeHandle(
                    Guid.NewGuid(),
                    "KRX-TEST",
                    workspace,
                    repositoryPath,
                    "kronxy/jobs/context-test",
                    new string('a', 40),
                    RepositoryWorktreeOperationKind.Created);

            Executor =
                new RecordingExecutor();

            Client =
                new SecureContextGitClient(
                    Executor,
                    Repository,
                    "context-test");
        }

        public RepositoryWorktreeHandle Repository
        {
            get;
        }

        public RecordingExecutor Executor
        {
            get;
        }

        public SecureContextGitClient Client
        {
            get;
        }

        public SecureToolResult Success(
            string stdout,
            SecureToolOperation operation) =>
            Result(
                ToolExecutionOutcome.Completed,
                0,
                stdout,
                string.Empty,
                operation,
                string.Empty);

        public SecureToolResult Failure(
            SecureToolOperation operation) =>
            Result(
                ToolExecutionOutcome.NonZeroExitCode,
                1,
                string.Empty,
                "failure",
                operation,
                "TOOL_NONZERO_EXIT");

        private SecureToolResult Result(
            ToolExecutionOutcome outcome,
            int? exitCode,
            string stdout,
            string stderr,
            SecureToolOperation operation,
            string errorCode)
        {
            DateTime now =
                DateTime.UtcNow;

            return new SecureToolResult(
                outcome,
                exitCode,
                stdout,
                stderr,
                new ToolExecutionAudit(
                    Repository.JobId,
                    Repository.JobExternalId,
                    operation,
                    Repository.WorkspacePath,
                    Repository.RepositoryPath,
                    "context-test",
                    now,
                    now,
                    TimeSpan.Zero,
                    exitCode,
                    outcome),
                errorCode);
        }
    }

    private sealed class RecordingExecutor :
        ISecureToolExecutor
    {
        public List<SecureToolRequest> Requests
        {
            get;
        } = [];

        public SecureToolResult? NextResult
        {
            get;
            set;
        }

        public Task<SecureToolResult> ExecuteAsync(
            SecureToolRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);

            return Task.FromResult(
                NextResult ??
                throw new InvalidOperationException(
                    "No fake result configured."));
        }
    }
}
