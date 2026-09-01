using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;
using Kronxy.Application.Workspaces;

namespace Kronxy.Infrastructure.Execution;

public sealed class ExecutionPlaneLifecycle :
    IExecutionPlaneLifecycle
{
    private readonly IWorkspaceManager workspaceManager;
    private readonly IRepositoryManager repositoryManager;

    public ExecutionPlaneLifecycle(
        IWorkspaceManager workspaceManager,
        IRepositoryManager repositoryManager)
    {
        this.workspaceManager =
            workspaceManager ??
            throw new ArgumentNullException(
                nameof(workspaceManager));

        this.repositoryManager =
            repositoryManager ??
            throw new ArgumentNullException(
                nameof(repositoryManager));
    }

    public async Task<ExecutionPlaneLifecycleResult>
        PrepareAsync(
            Guid jobId,
            string jobExternalId,
            string expectedHead,
            CancellationToken cancellationToken = default)
    {
        var workspaceResult =
            await workspaceManager
                .PrepareAsync(
                    jobId,
                    jobExternalId,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!workspaceResult.IsSuccess ||
            workspaceResult.Workspace is null)
        {
            return WorkspaceFailure(
                workspaceResult.ErrorCode);
        }

        var repositoryResult =
            await repositoryManager
                .PrepareWorktreeAsync(
                    workspaceResult.Workspace,
                    expectedHead,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!repositoryResult.IsSuccess ||
            repositoryResult.Value is null)
        {
            // Preserve the workspace on failure.
            // Recovery is safer than destructive rollback.
            return RepositoryFailure(
                repositoryResult.ErrorCode);
        }

        return ExecutionPlaneLifecycleResult.Success(
            new ExecutionPlaneSession(
                workspaceResult.Workspace,
                repositoryResult.Value));
    }

    public async Task<ExecutionPlaneLifecycleResult>
        RecoverAsync(
            Guid jobId,
            string jobExternalId,
            CancellationToken cancellationToken = default)
    {
        var workspaceResult =
            await workspaceManager
                .RecoverAsync(
                    jobId,
                    jobExternalId,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!workspaceResult.IsSuccess ||
            workspaceResult.Workspace is null)
        {
            return WorkspaceFailure(
                workspaceResult.ErrorCode);
        }

        var repositoryResult =
            await repositoryManager
                .RecoverWorktreeAsync(
                    workspaceResult.Workspace,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!repositoryResult.IsSuccess ||
            repositoryResult.Value is null)
        {
            return RepositoryFailure(
                repositoryResult.ErrorCode);
        }

        return ExecutionPlaneLifecycleResult.Success(
            new ExecutionPlaneSession(
                workspaceResult.Workspace,
                repositoryResult.Value));
    }

    public async Task<ExecutionPlaneLifecycleResult>
        CleanupAsync(
            Guid jobId,
            string jobExternalId,
            CancellationToken cancellationToken = default)
    {
        /*
         * Cleanup is intentionally ordered:
         *
         * 1. Recover ownership of the workspace.
         * 2. Remove the Git worktree safely.
         * 3. Remove the workspace only after Git succeeded.
         *
         * Never delete the workspace first because that can leave
         * stale Git worktree metadata in the authoritative repository.
         */

        var workspaceResult =
            await workspaceManager
                .RecoverAsync(
                    jobId,
                    jobExternalId,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!workspaceResult.IsSuccess ||
            workspaceResult.Workspace is null)
        {
            return WorkspaceFailure(
                workspaceResult.ErrorCode);
        }

        var repositoryResult =
            await repositoryManager
                .CleanupWorktreeAsync(
                    workspaceResult.Workspace,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!repositoryResult.IsSuccess ||
            repositoryResult.Value is null)
        {
            // Fail closed.
            // A dirty or invalid worktree preserves the workspace.
            return RepositoryFailure(
                repositoryResult.ErrorCode);
        }

        var workspaceCleanup =
            await workspaceManager
                .CleanupAsync(
                    jobId,
                    jobExternalId,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!workspaceCleanup.IsSuccess ||
            workspaceCleanup.Workspace is null)
        {
            return WorkspaceFailure(
                workspaceCleanup.ErrorCode);
        }

        return ExecutionPlaneLifecycleResult.Success(
            new ExecutionPlaneSession(
                workspaceCleanup.Workspace,
                repositoryResult.Value));
    }

    private static ExecutionPlaneLifecycleResult
        WorkspaceFailure(
            string code) =>
        ExecutionPlaneLifecycleResult.Failure(
            ExecutionPlaneFailureKind.WorkspaceFailure,
            string.IsNullOrWhiteSpace(code)
                ? "EXECUTION_WORKSPACE_FAILURE"
                : code);

    private static ExecutionPlaneLifecycleResult
        RepositoryFailure(
            string code) =>
        ExecutionPlaneLifecycleResult.Failure(
            ExecutionPlaneFailureKind.RepositoryFailure,
            string.IsNullOrWhiteSpace(code)
                ? "EXECUTION_REPOSITORY_FAILURE"
                : code);
}
