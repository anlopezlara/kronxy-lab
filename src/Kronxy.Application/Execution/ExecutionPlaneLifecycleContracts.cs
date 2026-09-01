using Kronxy.Application.Repositories;
using Kronxy.Application.Workspaces;

namespace Kronxy.Application.Execution;

public enum ExecutionPlaneFailureKind
{
    None = 0,
    WorkspaceFailure = 10,
    RepositoryFailure = 20
}

public sealed record ExecutionPlaneSession(
    WorkspaceHandle Workspace,
    RepositoryWorktreeHandle Repository);

public sealed record ExecutionPlaneLifecycleResult(
    ExecutionPlaneSession? Session,
    ExecutionPlaneFailureKind FailureKind,
    string ErrorCode)
{
    public bool IsSuccess =>
        FailureKind == ExecutionPlaneFailureKind.None;

    public static ExecutionPlaneLifecycleResult Success(
        ExecutionPlaneSession session) =>
        new(
            session,
            ExecutionPlaneFailureKind.None,
            string.Empty);

    public static ExecutionPlaneLifecycleResult Failure(
        ExecutionPlaneFailureKind kind,
        string errorCode) =>
        new(
            null,
            kind,
            errorCode);
}

public interface IExecutionPlaneLifecycle
{
    Task<ExecutionPlaneLifecycleResult> PrepareAsync(
        Guid jobId,
        string jobExternalId,
        string expectedHead,
        CancellationToken cancellationToken = default);

    Task<ExecutionPlaneLifecycleResult> RecoverAsync(
        Guid jobId,
        string jobExternalId,
        CancellationToken cancellationToken = default);

    Task<ExecutionPlaneLifecycleResult> CleanupAsync(
        Guid jobId,
        string jobExternalId,
        CancellationToken cancellationToken = default);
}
