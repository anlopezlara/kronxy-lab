using System.Security.Cryptography;
using System.Text;
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

    public int AttemptCount { get; init; }

    public string ProposalLineageId { get; init; } =
        string.Empty;

    public bool IsBuildCorrection { get; init; }
    public bool IsBuildCorrectionRetry { get; init; }
    public bool IsHumanReviewCorrection { get; init; }
    public bool IsGovernedHumanCorrection { get; init; }
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

public static class SafeChangeProposalIdentity
{
    public const int CurrentVersion = 2;

    private static readonly UTF8Encoding StrictUtf8 =
        new(false, true);

    public static string Fingerprint(
        ValidatedDeveloperProposal proposal,
        int schemaVersion = CurrentVersion)
    {
        using var memory = new MemoryStream();
        using (var writer = new BinaryWriter(
            memory,
            StrictUtf8,
            leaveOpen: true))
        {
            writer.Write(schemaVersion);
            WriteString(writer, proposal.Summary);
            writer.Write(proposal.Changes.Count);
            foreach (ValidatedDeveloperChange change in proposal.Changes)
            {
                writer.Write((int)change.Operation);
                WriteString(writer, change.RelativePath);
                WriteString(writer, change.Intent);
                WriteString(writer, change.Content);
                WriteString(
                    writer,
                    change.ExpectedContentSha256.ToLowerInvariant());
            }
            WriteStrings(writer, proposal.Assumptions);
            WriteStrings(writer, proposal.Risks);
            writer.Flush();
        }

        return Convert.ToHexString(
                SHA256.HashData(memory.ToArray()))
            .ToLowerInvariant();
    }

    private static void WriteStrings(
        BinaryWriter writer,
        IReadOnlyList<string> values)
    {
        writer.Write(values.Count);
        foreach (string value in values)
            WriteString(writer, value);
    }

    private static void WriteString(
        BinaryWriter writer,
        string value)
    {
        byte[] bytes = StrictUtf8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}
