namespace Kronxy.Application.Execution;

public enum RecoveryStage
{
    Context = 10,
    Planning = 20,
    Restore = 30,
    Build = 40,
    Test = 50
}

public enum StageRecoveryStatus
{
    NotCompleted = 0,
    Completed = 10,
    InvalidEvidence = 20,
    Cancelled = 30,
    Failure = 40
}

public sealed record StageRecoveryRequest
{
    public required Guid JobId { get; init; }

    public required Guid RunId { get; init; }

    public required RecoveryStage Stage { get; init; }

    public string CorrelationId { get; init; } =
        string.Empty;
}

public sealed record StageRecoveryResult(
    StageRecoveryStatus Status,
    string ErrorCode)
{
    public bool IsCompleted =>
        Status == StageRecoveryStatus.Completed;

    public static StageRecoveryResult Completed() =>
        new(
            StageRecoveryStatus.Completed,
            string.Empty);

    public static StageRecoveryResult NotCompleted() =>
        new(
            StageRecoveryStatus.NotCompleted,
            string.Empty);

    public static StageRecoveryResult InvalidEvidence(
        string errorCode) =>
        new(
            StageRecoveryStatus.InvalidEvidence,
            errorCode);

    public static StageRecoveryResult Cancelled(
        string errorCode) =>
        new(
            StageRecoveryStatus.Cancelled,
            errorCode);

    public static StageRecoveryResult Failure(
        string errorCode) =>
        new(
            StageRecoveryStatus.Failure,
            errorCode);
}

public interface IStageRecoveryEvidenceService
{
    Task<StageRecoveryResult> CheckAsync(
        StageRecoveryRequest request,
        CancellationToken cancellationToken = default);
}
