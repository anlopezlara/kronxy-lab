namespace Kronxy.Application.Workspaces;

public enum WorkspaceOperationKind
{
    Created = 0,
    Existing = 10,
    Recovered = 20,
    Removed = 30,
    AlreadyAbsent = 40
}

public enum WorkspaceFailureKind
{
    None = 0,
    InvalidJobIdentity = 10,
    UnsafeRoot = 20,
    UnsafePath = 30,
    Collision = 40,
    OwnershipMismatch = 50,
    IoFailure = 60
}

public sealed record WorkspaceHandle(
    Guid JobId,
    string JobExternalId,
    string Path,
    WorkspaceOperationKind Operation);

public sealed record WorkspaceOperationResult(
    WorkspaceHandle? Workspace,
    WorkspaceFailureKind FailureKind,
    string ErrorCode)
{
    public bool IsSuccess => FailureKind == WorkspaceFailureKind.None;

    public static WorkspaceOperationResult Success(
        WorkspaceHandle workspace) =>
        new(workspace, WorkspaceFailureKind.None, string.Empty);

    public static WorkspaceOperationResult Failure(
        WorkspaceFailureKind kind,
        string errorCode) =>
        new(null, kind, errorCode);
}

public interface IWorkspaceManager
{
    Task<WorkspaceOperationResult> PrepareAsync(
        Guid jobId,
        string jobExternalId,
        CancellationToken cancellationToken = default);

    Task<WorkspaceOperationResult> RecoverAsync(
        Guid jobId,
        string jobExternalId,
        CancellationToken cancellationToken = default);

    Task<WorkspaceOperationResult> CleanupAsync(
        Guid jobId,
        string jobExternalId,
        CancellationToken cancellationToken = default);
}
