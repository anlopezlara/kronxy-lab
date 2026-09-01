using Kronxy.Application.Repositories;

namespace Kronxy.Application.Execution;

public enum SecureToolOperation
{
    DotnetRestore = 10,
    DotnetBuild = 20,
    DotnetTest = 30,
    GitStatus = 40,
    GitDiff = 50,
    GitDiffStat = 60,
    GitListIndex = 70,
    GitListUntracked = 80
}

public enum ToolExecutionOutcome
{
    Completed = 0,
    NonZeroExitCode = 10,
    Rejected = 20,
    TimedOut = 30,
    Cancelled = 40,
    OutputLimitExceeded = 50,
    StartFailure = 60
}

public sealed record SecureToolRequest
{
    public required RepositoryWorktreeHandle Repository { get; init; }

    public required SecureToolOperation Operation { get; init; }

    public string? Target { get; init; }

    public string CorrelationId { get; init; } = string.Empty;

    public TimeSpan Timeout { get; init; } =
        TimeSpan.FromMinutes(5);

    public int StandardOutputLimitBytes { get; init; } =
        1_048_576;

    public int StandardErrorLimitBytes { get; init; } =
        1_048_576;
}

public sealed record ToolExecutionAudit(
    Guid JobId,
    string JobExternalId,
    SecureToolOperation Operation,
    string WorkspacePath,
    string RepositoryPath,
    string CorrelationId,
    DateTime StartedOnUtc,
    DateTime EndedOnUtc,
    TimeSpan Duration,
    int? ExitCode,
    ToolExecutionOutcome Outcome);

public sealed record SecureToolResult(
    ToolExecutionOutcome Outcome,
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    ToolExecutionAudit Audit,
    string ErrorCode)
{
    public bool IsSuccess =>
        Outcome == ToolExecutionOutcome.Completed &&
        ExitCode == 0;
}

public interface ISecureToolExecutor
{
    Task<SecureToolResult> ExecuteAsync(
        SecureToolRequest request,
        CancellationToken cancellationToken = default);
}
