namespace Kronxy.Infrastructure.Git;

internal enum SecureGitOperation
{
    ResolveCommit = 10,
    ShowTopLevel = 20,
    CommonDirectory = 30,
    CurrentBranch = 40,
    BranchExists = 50,
    WorktreeAddExistingBranch = 60,
    WorktreeAddNewBranch = 70,
    WorktreePrune = 80,
    WorktreeRemove = 90,
    Status = 100,
    Diff = 110,
    DiffStat = 120
}

internal sealed record SecureGitRequest
{
    public required SecureGitOperation Operation { get; init; }

    public required string WorkingDirectory { get; init; }

    public string? Reference { get; init; }

    public string? Branch { get; init; }

    public string? WorktreePath { get; init; }

    public string? Commit { get; init; }

    public TimeSpan Timeout { get; init; } =
        TimeSpan.FromSeconds(30);

    public int StandardOutputLimitBytes { get; init; } =
        1_048_576;

    public int StandardErrorLimitBytes { get; init; } =
        1_048_576;
}

internal enum SecureGitOutcome
{
    Completed = 0,
    NonZeroExitCode = 10,
    Rejected = 20,
    TimedOut = 30,
    Cancelled = 40,
    OutputLimitExceeded = 50,
    StartFailure = 60
}

internal sealed record SecureGitResult(
    SecureGitOutcome Outcome,
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    string ErrorCode)
{
    public bool IsSuccess =>
        Outcome == SecureGitOutcome.Completed &&
        ExitCode == 0;
}

internal interface ISecureGitExecutor
{
    Task<SecureGitResult> ExecuteAsync(
        SecureGitRequest request,
        CancellationToken cancellationToken = default);
}
