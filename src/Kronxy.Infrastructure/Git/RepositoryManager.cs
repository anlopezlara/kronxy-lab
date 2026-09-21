using System.Collections.Concurrent;
using System.Text.Json;
using Kronxy.Application.Repositories;
using Kronxy.Application.Workspaces;

namespace Kronxy.Infrastructure.Git;

public sealed class RepositoryManager : IRepositoryManager
{
    private const string MarkerFileName =
        ".kronxy-workspace.json";

    private const int GitOutputLimitBytes =
        8 * 1024 * 1024;

    private static readonly TimeSpan GitTimeout =
        TimeSpan.FromSeconds(30);

    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim>
        JobLocks = new();

    private readonly string authoritativeRepositoryRoot;
    private readonly string workspaceRoot;
    private readonly ISecureGitExecutor
        secureGitExecutor;

    public RepositoryManager(
        string authoritativeRepositoryRoot,
        string workspaceRoot,
        string gitExecutable)
    {
        this.authoritativeRepositoryRoot =
            ValidateAbsoluteRoot(
                authoritativeRepositoryRoot,
                nameof(authoritativeRepositoryRoot));

        this.workspaceRoot =
            ValidateAbsoluteRoot(
                workspaceRoot,
                nameof(workspaceRoot));

        secureGitExecutor =
            new SecureGitExecutor(
                gitExecutable);
    }

    public async Task<
        RepositoryOperationResult<RepositoryWorktreeHandle>>
        PrepareWorktreeAsync(
            WorkspaceHandle workspace,
            string expectedHead,
            CancellationToken cancellationToken = default)
    {
        if (!TryValidateRequest(
                workspace,
                out var repositoryPath,
                out var branch))
        {
            return FailureHandle(
                RepositoryFailureKind.InvalidRequest,
                "REPOSITORY_INVALID_REQUEST");
        }

        var gate = JobLocks.GetOrAdd(
            workspace.JobId,
            static _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var safety =
                await ValidateSafetyAsync(
                    workspace,
                    repositoryPath,
                    cancellationToken)
                    .ConfigureAwait(false);

            if (safety is not null)
            {
                return safety;
            }

            var canonicalHeadResult =
                await ResolveCommitAsync(
                    authoritativeRepositoryRoot,
                    expectedHead,
                    cancellationToken)
                    .ConfigureAwait(false);

            if (!canonicalHeadResult.IsSuccess)
            {
                return RepositoryOperationResult<
                    RepositoryWorktreeHandle>.Failure(
                        canonicalHeadResult.FailureKind,
                        canonicalHeadResult.ErrorCode);
            }

            if (Directory.Exists(repositoryPath))
            {
                var existing =
                    await ValidateExistingWorktreeAsync(
                        workspace,
                        repositoryPath,
                        branch,
                        RepositoryWorktreeOperationKind.Existing,
                        cancellationToken)
                        .ConfigureAwait(false);

                return existing;
            }

            var branchExists =
                await BranchExistsAsync(
                    branch,
                    cancellationToken)
                    .ConfigureAwait(false);

            GitCommandResult createResult;

            if (branchExists)
            {
                createResult =
                    await RunGitAsync(
                        authoritativeRepositoryRoot,
                        SecureGitOperation
                            .WorktreeAddExistingBranch,
                        cancellationToken,
                        branch: branch,
                        worktreePath: repositoryPath)
                        .ConfigureAwait(false);
            }
            else
            {
                createResult =
                    await RunGitAsync(
                        authoritativeRepositoryRoot,
                        SecureGitOperation
                            .WorktreeAddNewBranch,
                        cancellationToken,
                        branch: branch,
                        worktreePath: repositoryPath,
                        commit:
                            canonicalHeadResult.Value!)
                        .ConfigureAwait(false);
            }

            if (!createResult.IsSuccess)
            {
                return MapGitFailure(
                    createResult,
                    "REPOSITORY_WORKTREE_CREATE_FAILED");
            }

            return await ValidateExistingWorktreeAsync(
                    workspace,
                    repositoryPath,
                    branch,
                    RepositoryWorktreeOperationKind.Created,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<
        RepositoryOperationResult<RepositoryWorktreeHandle>>
        RecoverWorktreeAsync(
            WorkspaceHandle workspace,
            CancellationToken cancellationToken = default)
    {
        if (!TryValidateRequest(
                workspace,
                out var repositoryPath,
                out var branch))
        {
            return FailureHandle(
                RepositoryFailureKind.InvalidRequest,
                "REPOSITORY_INVALID_REQUEST");
        }

        var gate = JobLocks.GetOrAdd(
            workspace.JobId,
            static _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var safety =
                await ValidateSafetyAsync(
                    workspace,
                    repositoryPath,
                    cancellationToken)
                    .ConfigureAwait(false);

            if (safety is not null)
            {
                return safety;
            }

            if (!Directory.Exists(repositoryPath))
            {
                return FailureHandle(
                    RepositoryFailureKind.WorktreeConflict,
                    "REPOSITORY_WORKTREE_NOT_FOUND");
            }

            return await ValidateExistingWorktreeAsync(
                    workspace,
                    repositoryPath,
                    branch,
                    RepositoryWorktreeOperationKind.Recovered,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<RepositoryOperationResult<string>>
        GetHeadAsync(
            WorkspaceHandle workspace,
            CancellationToken cancellationToken = default)
    {
        var validation =
            await ValidateReadableWorktreeAsync(
                workspace,
                cancellationToken)
                .ConfigureAwait(false);

        if (!validation.IsSuccess)
        {
            return RepositoryOperationResult<string>.Failure(
                validation.FailureKind,
                validation.ErrorCode);
        }

        return await ResolveCommitAsync(
                validation.Value!,
                "HEAD",
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<RepositoryOperationResult<string>>
        GetCurrentBranchAsync(
            WorkspaceHandle workspace,
            CancellationToken cancellationToken = default)
    {
        var validation =
            await ValidateReadableWorktreeAsync(
                workspace,
                cancellationToken)
                .ConfigureAwait(false);

        if (!validation.IsSuccess)
        {
            return RepositoryOperationResult<string>.Failure(
                validation.FailureKind,
                validation.ErrorCode);
        }

        var result =
            await RunGitAsync(
                validation.Value!,
                SecureGitOperation.CurrentBranch,
                cancellationToken)
                .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return MapGitFailure<string>(
                result,
                "REPOSITORY_BRANCH_QUERY_FAILED");
        }

        return RepositoryOperationResult<string>.Success(
            ParseSingleLine(result.StandardOutput));
    }

    public async Task<
        RepositoryOperationResult<RepositoryStatus>>
        GetStatusAsync(
            WorkspaceHandle workspace,
            CancellationToken cancellationToken = default)
    {
        var validation =
            await ValidateReadableWorktreeAsync(
                workspace,
                cancellationToken)
                .ConfigureAwait(false);

        if (!validation.IsSuccess)
        {
            return RepositoryOperationResult<
                RepositoryStatus>.Failure(
                    validation.FailureKind,
                    validation.ErrorCode);
        }

        var result =
            await RunGitAsync(
                validation.Value!,
                SecureGitOperation.Status,
                cancellationToken)
                .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return MapGitFailure<RepositoryStatus>(
                result,
                "REPOSITORY_STATUS_FAILED");
        }

        return RepositoryOperationResult<
            RepositoryStatus>.Success(
                new RepositoryStatus(
                    string.IsNullOrEmpty(
                        result.StandardOutput),
                    result.StandardOutput));
    }

    public async Task<RepositoryOperationResult<IReadOnlyList<ObservedRepositoryChange>>>
        GetObservedChangesAsync(
            WorkspaceHandle workspace,
            CancellationToken cancellationToken = default)
    {
        var validation = await ValidateReadableWorktreeAsync(
            workspace, cancellationToken).ConfigureAwait(false);
        if (!validation.IsSuccess)
            return RepositoryOperationResult<IReadOnlyList<ObservedRepositoryChange>>.Failure(
                validation.FailureKind, validation.ErrorCode);

        var result = await RunGitAsync(
            validation.Value!, SecureGitOperation.StatusNull,
            cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
            return RepositoryOperationResult<IReadOnlyList<ObservedRepositoryChange>>.Failure(
                result.OutputLimitExceeded ? RepositoryFailureKind.OutputLimitExceeded : RepositoryFailureKind.GitFailure,
                "REPOSITORY_OBSERVED_CHANGES_FAILED");

        return ObservedGitStatusParser.Parse(result.StandardOutput);
    }

    public async Task<RepositoryOperationResult<string>>
        GetDiffAsync(
            WorkspaceHandle workspace,
            CancellationToken cancellationToken = default)
    {
        return await GetTextGitResultAsync(
                workspace,
                SecureGitOperation.Diff,
                "REPOSITORY_DIFF_FAILED",
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<RepositoryOperationResult<string>>
        GetDiffStatAsync(
            WorkspaceHandle workspace,
            CancellationToken cancellationToken = default)
    {
        return await GetTextGitResultAsync(
                workspace,
                SecureGitOperation.DiffStat,
                "REPOSITORY_DIFF_STAT_FAILED",
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<
        RepositoryOperationResult<RepositoryWorktreeHandle>>
        CleanupWorktreeAsync(
            WorkspaceHandle workspace,
            CancellationToken cancellationToken = default)
    {
        if (!TryValidateRequest(
                workspace,
                out var repositoryPath,
                out var branch))
        {
            return FailureHandle(
                RepositoryFailureKind.InvalidRequest,
                "REPOSITORY_INVALID_REQUEST");
        }

        var gate = JobLocks.GetOrAdd(
            workspace.JobId,
            static _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var safety =
                await ValidateSafetyAsync(
                    workspace,
                    repositoryPath,
                    cancellationToken)
                    .ConfigureAwait(false);

            if (safety is not null)
            {
                return safety;
            }

            if (!Directory.Exists(repositoryPath))
            {
                await RunGitAsync(
                        authoritativeRepositoryRoot,
                        SecureGitOperation.WorktreePrune,
                        cancellationToken)
                    .ConfigureAwait(false);

                return RepositoryOperationResult<
                    RepositoryWorktreeHandle>.Success(
                        new RepositoryWorktreeHandle(
                            workspace.JobId,
                            workspace.JobExternalId,
                            workspace.Path,
                            repositoryPath,
                            branch,
                            string.Empty,
                            RepositoryWorktreeOperationKind
                                .AlreadyAbsent));
            }

            var existing =
                await ValidateExistingWorktreeAsync(
                    workspace,
                    repositoryPath,
                    branch,
                    RepositoryWorktreeOperationKind.Existing,
                    cancellationToken)
                    .ConfigureAwait(false);

            if (!existing.IsSuccess)
            {
                return existing;
            }

            var status =
                await GetStatusAsync(
                    workspace,
                    cancellationToken)
                    .ConfigureAwait(false);

            if (!status.IsSuccess)
            {
                return FailureHandle(
                    status.FailureKind,
                    status.ErrorCode);
            }

            if (!status.Value!.IsClean)
            {
                return FailureHandle(
                    RepositoryFailureKind.DirtyWorktree,
                    "REPOSITORY_WORKTREE_DIRTY");
            }

            var head =
                existing.Value!.Head;

            var remove =
                await RunGitAsync(
                    authoritativeRepositoryRoot,
                    SecureGitOperation.WorktreeRemove,
                    cancellationToken,
                    worktreePath: repositoryPath)
                    .ConfigureAwait(false);

            if (!remove.IsSuccess)
            {
                return MapGitFailure(
                    remove,
                    "REPOSITORY_WORKTREE_REMOVE_FAILED");
            }

            return RepositoryOperationResult<
                RepositoryWorktreeHandle>.Success(
                    new RepositoryWorktreeHandle(
                        workspace.JobId,
                        workspace.JobExternalId,
                        workspace.Path,
                        repositoryPath,
                        branch,
                        head,
                        RepositoryWorktreeOperationKind.Removed));
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<
        RepositoryOperationResult<string>>
        ValidateReadableWorktreeAsync(
            WorkspaceHandle workspace,
            CancellationToken cancellationToken)
    {
        if (!TryValidateRequest(
                workspace,
                out var repositoryPath,
                out var branch))
        {
            return RepositoryOperationResult<string>.Failure(
                RepositoryFailureKind.InvalidRequest,
                "REPOSITORY_INVALID_REQUEST");
        }

        var safety =
            await ValidateSafetyAsync(
                workspace,
                repositoryPath,
                cancellationToken)
                .ConfigureAwait(false);

        if (safety is not null)
        {
            return RepositoryOperationResult<string>.Failure(
                safety.FailureKind,
                safety.ErrorCode);
        }

        if (!Directory.Exists(repositoryPath))
        {
            return RepositoryOperationResult<string>.Failure(
                RepositoryFailureKind.WorktreeConflict,
                "REPOSITORY_WORKTREE_NOT_FOUND");
        }

        var existing =
            await ValidateExistingWorktreeAsync(
                workspace,
                repositoryPath,
                branch,
                RepositoryWorktreeOperationKind.Existing,
                cancellationToken)
                .ConfigureAwait(false);

        if (!existing.IsSuccess)
        {
            return RepositoryOperationResult<string>.Failure(
                existing.FailureKind,
                existing.ErrorCode);
        }

        return RepositoryOperationResult<string>.Success(
            repositoryPath);
    }

    private async Task<RepositoryOperationResult<string>>
        GetTextGitResultAsync(
            WorkspaceHandle workspace,
            SecureGitOperation operation,
            string failureCode,
            CancellationToken cancellationToken)
    {
        var validation =
            await ValidateReadableWorktreeAsync(
                workspace,
                cancellationToken)
                .ConfigureAwait(false);

        if (!validation.IsSuccess)
        {
            return RepositoryOperationResult<string>.Failure(
                validation.FailureKind,
                validation.ErrorCode);
        }

        var result =
            await RunGitAsync(
                validation.Value!,
                operation,
                cancellationToken)
                .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return MapGitFailure<string>(
                result,
                failureCode);
        }

        return RepositoryOperationResult<string>.Success(
            result.StandardOutput);
    }

    private async Task<
        RepositoryOperationResult<RepositoryWorktreeHandle>?>
        ValidateSafetyAsync(
            WorkspaceHandle workspace,
            string repositoryPath,
            CancellationToken cancellationToken)
    {
        if (!Directory.Exists(
                authoritativeRepositoryRoot))
        {
            return FailureHandle(
                RepositoryFailureKind.UnsafeRepository,
                "REPOSITORY_AUTHORITATIVE_ROOT_NOT_FOUND");
        }

        if (!Directory.Exists(workspaceRoot) ||
            !Directory.Exists(workspace.Path))
        {
            return FailureHandle(
                RepositoryFailureKind.UnsafeWorkspace,
                "REPOSITORY_WORKSPACE_NOT_FOUND");
        }

        if (ContainsLinkInPath(
                authoritativeRepositoryRoot) ||
            ContainsLinkInPath(workspaceRoot) ||
            IsLink(new DirectoryInfo(workspace.Path)))
        {
            return FailureHandle(
                RepositoryFailureKind.UnsafeWorkspace,
                "REPOSITORY_SYMLINK_PATH_REJECTED");
        }

        if (!ValidateWorkspaceMarker(workspace))
        {
            return FailureHandle(
                RepositoryFailureKind.UnsafeWorkspace,
                "REPOSITORY_WORKSPACE_OWNERSHIP_INVALID");
        }

        if (!IsDirectChild(
                workspaceRoot,
                workspace.Path) ||
            !IsDirectChild(
                workspace.Path,
                repositoryPath))
        {
            return FailureHandle(
                RepositoryFailureKind.UnsafeWorkspace,
                "REPOSITORY_WORKSPACE_PATH_INVALID");
        }

        var rootResult =
            await RunGitAsync(
                authoritativeRepositoryRoot,
                SecureGitOperation.ShowTopLevel,
                cancellationToken)
                .ConfigureAwait(false);

        if (!rootResult.IsSuccess)
        {
            return FailureHandle(
                RepositoryFailureKind.UnsafeRepository,
                "REPOSITORY_AUTHORITATIVE_NOT_GIT");
        }

        string discovered;

        try
        {
            discovered =
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(
                        ParseSingleLine(
                            rootResult.StandardOutput)));
        }
        catch
        {
            return FailureHandle(
                RepositoryFailureKind.UnsafeRepository,
                "REPOSITORY_AUTHORITATIVE_ROOT_INVALID");
        }

        if (!PathEquals(
                discovered,
                authoritativeRepositoryRoot))
        {
            return FailureHandle(
                RepositoryFailureKind.UnsafeRepository,
                "REPOSITORY_AUTHORITATIVE_ROOT_MISMATCH");
        }

        return null;
    }

    private async Task<
        RepositoryOperationResult<RepositoryWorktreeHandle>>
        ValidateExistingWorktreeAsync(
            WorkspaceHandle workspace,
            string repositoryPath,
            string expectedBranch,
            RepositoryWorktreeOperationKind operation,
            CancellationToken cancellationToken)
    {
        if (IsLink(new DirectoryInfo(repositoryPath)))
        {
            return FailureHandle(
                RepositoryFailureKind.WorktreeConflict,
                "REPOSITORY_WORKTREE_IS_LINK");
        }

        var rootResult =
            await RunGitAsync(
                repositoryPath,
                SecureGitOperation.ShowTopLevel,
                cancellationToken)
                .ConfigureAwait(false);

        if (!rootResult.IsSuccess)
        {
            return FailureHandle(
                RepositoryFailureKind.WorktreeConflict,
                "REPOSITORY_WORKTREE_NOT_GIT");
        }

        var discoveredRoot =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(
                    ParseSingleLine(
                        rootResult.StandardOutput)));

        if (!PathEquals(
                discoveredRoot,
                repositoryPath))
        {
            return FailureHandle(
                RepositoryFailureKind.WorktreeConflict,
                "REPOSITORY_WORKTREE_ROOT_MISMATCH");
        }

        var commonDirectory =
            await GetCommonGitDirectoryAsync(
                repositoryPath,
                cancellationToken)
                .ConfigureAwait(false);

        var authoritativeCommonDirectory =
            await GetCommonGitDirectoryAsync(
                authoritativeRepositoryRoot,
                cancellationToken)
                .ConfigureAwait(false);

        if (!commonDirectory.IsSuccess ||
            !authoritativeCommonDirectory.IsSuccess ||
            !PathEquals(
                commonDirectory.Value!,
                authoritativeCommonDirectory.Value!))
        {
            return FailureHandle(
                RepositoryFailureKind.WorktreeConflict,
                "REPOSITORY_WORKTREE_FOREIGN");
        }

        var branchResult =
            await RunGitAsync(
                repositoryPath,
                SecureGitOperation.CurrentBranch,
                cancellationToken)
                .ConfigureAwait(false);

        if (!branchResult.IsSuccess)
        {
            return FailureHandle(
                RepositoryFailureKind.BranchConflict,
                "REPOSITORY_WORKTREE_DETACHED");
        }

        var actualBranch =
            ParseSingleLine(
                branchResult.StandardOutput);

        if (!string.Equals(
                actualBranch,
                expectedBranch,
                StringComparison.Ordinal))
        {
            return FailureHandle(
                RepositoryFailureKind.BranchConflict,
                "REPOSITORY_BRANCH_MISMATCH");
        }

        var headResult =
            await ResolveCommitAsync(
                repositoryPath,
                "HEAD",
                cancellationToken)
                .ConfigureAwait(false);

        if (!headResult.IsSuccess)
        {
            return FailureHandle(
                headResult.FailureKind,
                headResult.ErrorCode);
        }

        return RepositoryOperationResult<
            RepositoryWorktreeHandle>.Success(
                new RepositoryWorktreeHandle(
                    workspace.JobId,
                    workspace.JobExternalId,
                    workspace.Path,
                    repositoryPath,
                    actualBranch,
                    headResult.Value!,
                    operation));
    }

    private async Task<
        RepositoryOperationResult<string>>
        GetCommonGitDirectoryAsync(
            string workingDirectory,
            CancellationToken cancellationToken)
    {
        var result =
            await RunGitAsync(
                workingDirectory,
                SecureGitOperation.CommonDirectory,
                cancellationToken)
                .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return RepositoryOperationResult<string>.Failure(
                RepositoryFailureKind.GitFailure,
                "REPOSITORY_COMMON_DIR_FAILED");
        }

        var raw =
            ParseSingleLine(result.StandardOutput);

        var path =
            Path.IsPathFullyQualified(raw)
                ? Path.GetFullPath(raw)
                : Path.GetFullPath(
                    Path.Combine(
                        workingDirectory,
                        raw));

        return RepositoryOperationResult<string>.Success(
            Path.TrimEndingDirectorySeparator(path));
    }

    private async Task<
        RepositoryOperationResult<string>>
        ResolveCommitAsync(
            string workingDirectory,
            string reference,
            CancellationToken cancellationToken)
    {
        if (!IsValidReference(reference))
        {
            return RepositoryOperationResult<string>.Failure(
                RepositoryFailureKind.InvalidReference,
                "REPOSITORY_INVALID_REFERENCE");
        }

        var result =
            await RunGitAsync(
                workingDirectory,
                SecureGitOperation.ResolveCommit,
                cancellationToken,
                reference: reference)
                .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return MapGitFailure<string>(
                result,
                "REPOSITORY_REFERENCE_NOT_FOUND");
        }

        var value =
            ParseSingleLine(
                result.StandardOutput);

        if ((value.Length is not 40 and not 64) ||
            value.Any(
                character =>
                    !Uri.IsHexDigit(character)))
        {
            return RepositoryOperationResult<string>.Failure(
                RepositoryFailureKind.GitFailure,
                "REPOSITORY_INVALID_OBJECT_ID");
        }

        return RepositoryOperationResult<string>.Success(
            value.ToLowerInvariant());
    }

    private async Task<bool> BranchExistsAsync(
        string branch,
        CancellationToken cancellationToken)
    {
        var result =
            await RunGitAsync(
                authoritativeRepositoryRoot,
                SecureGitOperation.BranchExists,
                cancellationToken,
                branch: branch)
                .ConfigureAwait(false);

        return result.ExitCode == 0 &&
               !result.TimedOut &&
               !result.OutputLimitExceeded;
    }

    private bool TryValidateRequest(
        WorkspaceHandle? workspace,
        out string repositoryPath,
        out string branch)
    {
        repositoryPath = string.Empty;
        branch = string.Empty;

        if (workspace is null ||
            workspace.JobId == Guid.Empty ||
            !IsValidExternalId(
                workspace.JobExternalId) ||
            string.IsNullOrWhiteSpace(
                workspace.Path))
        {
            return false;
        }

        string fullWorkspacePath;

        try
        {
            fullWorkspacePath =
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(
                        workspace.Path));
        }
        catch
        {
            return false;
        }

        if (!PathEquals(
                fullWorkspacePath,
                workspace.Path))
        {
            return false;
        }

        repositoryPath =
            Path.GetFullPath(
                Path.Combine(
                    fullWorkspacePath,
                    "repository"));

        if (!IsDirectChild(
                fullWorkspacePath,
                repositoryPath))
        {
            return false;
        }

        branch =
            BuildBranchName(
                workspace.JobExternalId,
                workspace.JobId);

        return true;
    }

    private bool ValidateWorkspaceMarker(
        WorkspaceHandle workspace)
    {
        try
        {
            var markerPath =
                Path.Combine(
                    workspace.Path,
                    MarkerFileName);

            var info =
                new FileInfo(markerPath);

            if (!info.Exists ||
                IsLink(info))
            {
                return false;
            }

            using var stream =
                new FileStream(
                    markerPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    4096,
                    FileOptions.SequentialScan);

            using var document =
                JsonDocument.Parse(stream);

            var root =
                document.RootElement;

            if (!root.TryGetProperty(
                    "Version",
                    out var version) ||
                version.GetInt32() != 1 ||
                !root.TryGetProperty(
                    "JobId",
                    out var jobId) ||
                jobId.GetGuid() != workspace.JobId ||
                !root.TryGetProperty(
                    "JobExternalId",
                    out var externalId))
            {
                return false;
            }

            return string.Equals(
                externalId.GetString(),
                workspace.JobExternalId,
                StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private async Task<GitCommandResult>
        RunGitAsync(
            string workingDirectory,
            SecureGitOperation operation,
            CancellationToken cancellationToken,
            string? reference = null,
            string? branch = null,
            string? worktreePath = null,
            string? commit = null)
    {
        SecureGitResult result =
            await secureGitExecutor
                .ExecuteAsync(
                    new SecureGitRequest
                    {
                        Operation = operation,

                        WorkingDirectory =
                            workingDirectory,

                        Reference =
                            reference,

                        Branch =
                            branch,

                        WorktreePath =
                            worktreePath,

                        Commit =
                            commit,

                        Timeout =
                            GitTimeout,

                        StandardOutputLimitBytes =
                            GitOutputLimitBytes,

                        StandardErrorLimitBytes =
                            GitOutputLimitBytes
                    },
                    cancellationToken)
                .ConfigureAwait(false);

        if (result.Outcome ==
            SecureGitOutcome.Cancelled)
        {
            cancellationToken
                .ThrowIfCancellationRequested();
        }

        return new GitCommandResult(
            result.ExitCode,
            result.StandardOutput,
            result.StandardError,
            result.Outcome ==
                SecureGitOutcome.TimedOut,
            result.Outcome ==
                SecureGitOutcome.OutputLimitExceeded,
            result.Outcome ==
                SecureGitOutcome.StartFailure);
    }

    private static RepositoryOperationResult<
        RepositoryWorktreeHandle>
        MapGitFailure(
            GitCommandResult result,
            string errorCode)
    {
        return RepositoryOperationResult<
            RepositoryWorktreeHandle>.Failure(
                ClassifyFailure(result),
                errorCode);
    }

    private static RepositoryOperationResult<T>
        MapGitFailure<T>(
            GitCommandResult result,
            string errorCode)
    {
        return RepositoryOperationResult<T>.Failure(
            ClassifyFailure(result),
            errorCode);
    }

    private static RepositoryFailureKind
        ClassifyFailure(
            GitCommandResult result)
    {
        if (result.TimedOut)
        {
            return RepositoryFailureKind.TimedOut;
        }

        if (result.OutputLimitExceeded)
        {
            return RepositoryFailureKind
                .OutputLimitExceeded;
        }

        return RepositoryFailureKind.GitFailure;
    }

    private static RepositoryOperationResult<
        RepositoryWorktreeHandle>
        FailureHandle(
            RepositoryFailureKind kind,
            string errorCode)
    {
        return RepositoryOperationResult<
            RepositoryWorktreeHandle>.Failure(
                kind,
                errorCode);
    }

    private static string BuildBranchName(
        string externalId,
        Guid jobId)
    {
        return
            $"kronxy/jobs/{externalId.ToLowerInvariant()}-" +
            $"{jobId:N}"[..12];
    }

    private static bool IsValidExternalId(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        if (!trimmed.StartsWith(
                "KRX-",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var number =
            trimmed.AsSpan(4);

        return number.Length is >= 6 and <= 18 &&
               number.ToArray().All(
                   char.IsAsciiDigit);
    }

    private static bool IsValidReference(
        string? value)
    {
        return
            !string.IsNullOrWhiteSpace(value) &&
            value.Length <= 1024 &&
            value[0] != '-' &&
            value.IndexOfAny(
                ['\0', '\r', '\n']) < 0;
    }

    private static string ParseSingleLine(
        string value)
    {
        var parsed =
            value.TrimEnd('\r', '\n');

        if (string.IsNullOrEmpty(parsed) ||
            parsed.IndexOfAny(
                ['\0', '\r', '\n']) >= 0)
        {
            throw new InvalidOperationException(
                "Git returned malformed output.");
        }

        return parsed;
    }

    private static string ValidateAbsoluteRoot(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Path.IsPathFullyQualified(value) ||
            value.IndexOfAny(
                ['\0', '\r', '\n']) >= 0)
        {
            throw new ArgumentException(
                "A valid absolute path is required.",
                parameterName);
        }

        return Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(value));
    }

    private static bool IsDirectChild(
        string parent,
        string candidate)
    {
        var normalizedParent =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(parent));

        var normalizedCandidate =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(candidate));

        var candidateParent =
            Directory.GetParent(
                normalizedCandidate);

        return candidateParent is not null &&
               PathEquals(
                   candidateParent.FullName,
                   normalizedParent);
    }

    private static bool PathEquals(
        string left,
        string right)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(right)),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
    }

    private static bool ContainsLinkInPath(
        string path)
    {
        try
        {
            var current =
                new DirectoryInfo(
                    Path.GetFullPath(path));

            while (current is not null)
            {
                if (IsLink(current))
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }
        catch
        {
            return true;
        }
    }

    private static bool IsLink(
        FileSystemInfo info)
    {
        try
        {
            info.Refresh();

            return info.LinkTarget is not null ||
                   (info.Attributes &
                    FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return true;
        }
    }

    private sealed record GitCommandResult(
        int? ExitCode,
        string StandardOutput,
        string StandardError,
        bool TimedOut,
        bool OutputLimitExceeded,
        bool StartFailed)
    {
        public bool IsSuccess =>
            ExitCode == 0 &&
            !TimedOut &&
            !OutputLimitExceeded &&
            !StartFailed;
    }
}
