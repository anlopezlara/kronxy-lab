using Kronxy.Application.Workspaces;

namespace Kronxy.Application.Repositories;

public enum RepositoryWorktreeOperationKind
{
    Created = 0,
    Existing = 10,
    Recovered = 20,
    Removed = 30,
    AlreadyAbsent = 40
}

public enum RepositoryFailureKind
{
    None = 0,
    InvalidRequest = 10,
    UnsafeRepository = 20,
    UnsafeWorkspace = 30,
    InvalidReference = 40,
    BranchConflict = 50,
    WorktreeConflict = 60,
    DirtyWorktree = 70,
    TimedOut = 80,
    OutputLimitExceeded = 90,
    GitFailure = 100,
    UnsupportedObservedChange = 110
}

public sealed record RepositoryWorktreeHandle(
    Guid JobId,
    string JobExternalId,
    string WorkspacePath,
    string RepositoryPath,
    string Branch,
    string Head,
    RepositoryWorktreeOperationKind Operation);

public sealed record RepositoryStatus(
    bool IsClean,
    string Porcelain);

public enum ObservedRepositoryChangeKind
{
    Created = 10,
    Modified = 20,
    Deleted = 30
}

public sealed record ObservedRepositoryChange(
    ObservedRepositoryChangeKind ChangeKind,
    string RelativePath);

public sealed record RepositoryPreparedCommit(
    string CommitSha,
    string TreeSha,
    string ParentSha,
    string JobRef);

public sealed record RepositoryTargetRef(
    string RefName,
    string Head);

public sealed record RepositoryPromotionResult(
    string PreviousHead,
    string ResultingHead,
    bool AlreadyPromoted);

public sealed record RepositoryOperationResult<T>(
    T? Value,
    RepositoryFailureKind FailureKind,
    string ErrorCode)
{
    public bool IsSuccess =>
        FailureKind == RepositoryFailureKind.None;

    public static RepositoryOperationResult<T> Success(
        T value) =>
        new(value, RepositoryFailureKind.None, string.Empty);

    public static RepositoryOperationResult<T> Failure(
        RepositoryFailureKind kind,
        string errorCode) =>
        new(default, kind, errorCode);
}

public interface IRepositoryManager
{
    Task<RepositoryOperationResult<RepositoryWorktreeHandle>>
        PrepareWorktreeAsync(
            WorkspaceHandle workspace,
            string expectedHead,
            CancellationToken cancellationToken = default);

    Task<RepositoryOperationResult<RepositoryWorktreeHandle>>
        RecoverWorktreeAsync(
            WorkspaceHandle workspace,
            CancellationToken cancellationToken = default);

    Task<RepositoryOperationResult<string>>
        GetHeadAsync(
            WorkspaceHandle workspace,
            CancellationToken cancellationToken = default);

    Task<RepositoryOperationResult<string>>
        GetCurrentBranchAsync(
            WorkspaceHandle workspace,
            CancellationToken cancellationToken = default);

    Task<RepositoryOperationResult<RepositoryStatus>>
        GetStatusAsync(
            WorkspaceHandle workspace,
            CancellationToken cancellationToken = default);

    Task<RepositoryOperationResult<IReadOnlyList<ObservedRepositoryChange>>>
        GetObservedChangesAsync(
            WorkspaceHandle workspace,
            CancellationToken cancellationToken = default);

    Task<RepositoryOperationResult<string>>
        GetDiffAsync(
            WorkspaceHandle workspace,
            CancellationToken cancellationToken = default);

    Task<RepositoryOperationResult<string>>
        GetDiffStatAsync(
            WorkspaceHandle workspace,
            CancellationToken cancellationToken = default);

    Task<RepositoryOperationResult<RepositoryWorktreeHandle>>
        CleanupWorktreeAsync(
            WorkspaceHandle workspace,
            CancellationToken cancellationToken = default);

    Task<RepositoryOperationResult<RepositoryPreparedCommit>> PrepareCommitAsync(
        WorkspaceHandle workspace,
        string expectedBaseHead,
        IReadOnlyList<string> expectedChangedPaths,
        string message,
        string actor,
        DateTimeOffset recordedAtUtc,
        CancellationToken cancellationToken = default);

    Task<RepositoryOperationResult<RepositoryTargetRef>>
        GetAuthoritativeTargetRefAsync(
            CancellationToken cancellationToken = default);

    Task<RepositoryOperationResult<RepositoryPromotionResult>> PromoteAsync(
        string targetRef,
        string expectedTargetHead,
        string preparedCommitSha,
        string expectedCommitTree,
        string jobRef,
        IReadOnlyList<string> governedPaths,
        CancellationToken cancellationToken = default);
}
