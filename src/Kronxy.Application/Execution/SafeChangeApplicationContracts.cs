using Kronxy.Application.Repositories;

namespace Kronxy.Application.Execution;

public enum SafeChangeApplicationFailureKind
{
    None = 0,
    InvalidRequest = 10,
    UnsafeWorkspace = 20,
    InvalidPath = 30,
    ProtectedPath = 40,
    PreconditionFailed = 50,
    DestinationConflict = 60,
    SymlinkDetected = 70,
    LimitExceeded = 80,
    AtomicityFailure = 90,
    IoFailure = 100,
    Cancelled = 110
}

public sealed record AppliedFileChange(
    DeveloperChangeOperationType Operation,
    string RelativePath,
    string? BeforeSha256,
    string AfterSha256,
    long SizeBytes);

public sealed record SafeChangeApplicationRequest
{
    public required Guid JobId { get; init; }

    public required Guid RunId { get; init; }

    public required RepositoryWorktreeHandle Repository
    {
        get;
        init;
    }

    public required ValidatedDeveloperProposal Proposal
    {
        get;
        init;
    }

    public string CorrelationId { get; init; } =
        string.Empty;

    public bool IsBuildCorrection { get; init; }
    public bool IsHumanReviewCorrection { get; init; }
}

public sealed record SafeChangeApplicationReport(
    Guid JobId,
    Guid RunId,
    IReadOnlyList<AppliedFileChange> Changes,
    long TotalBytes,
    DateTime StartedOnUtc,
    DateTime EndedOnUtc)
{
    public bool IsSuccess =>
        JobId != Guid.Empty &&
        RunId != Guid.Empty &&
        Changes.Count > 0 &&
        TotalBytes >= 0 &&
        EndedOnUtc >= StartedOnUtc;
}

public sealed record SafeChangeApplicationResult(
    SafeChangeApplicationReport? Report,
    SafeChangeApplicationFailureKind FailureKind,
    string ErrorCode)
{
    public bool IsSuccess =>
        FailureKind ==
            SafeChangeApplicationFailureKind.None &&
        Report is not null &&
        Report.IsSuccess &&
        string.IsNullOrEmpty(ErrorCode);

    public static SafeChangeApplicationResult Success(
        SafeChangeApplicationReport report)
    {
        return new SafeChangeApplicationResult(
            report,
            SafeChangeApplicationFailureKind.None,
            string.Empty);
    }

    public static SafeChangeApplicationResult Failure(
        SafeChangeApplicationFailureKind failureKind,
        string errorCode)
    {
        return new SafeChangeApplicationResult(
            null,
            failureKind,
            errorCode);
    }
}

public interface ISafeChangeApplier
{
    Task<SafeChangeApplicationResult> ApplyAsync(
        SafeChangeApplicationRequest request,
        CancellationToken cancellationToken = default);
}
