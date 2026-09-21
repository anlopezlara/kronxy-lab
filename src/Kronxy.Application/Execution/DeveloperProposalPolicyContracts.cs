namespace Kronxy.Application.Execution;

public enum DeveloperProposalFailureKind
{
    None = 0,
    InvalidProposal = 10,
    UnsupportedOperation = 20,
    InvalidPath = 30,
    ProtectedPath = 40,
    DuplicatePath = 50,
    InvalidIntent = 60,
    InvalidExpectedHash = 70,
    TooManyOperations = 80,
    TooManyCreatedFiles = 90,
    FileTooLarge = 100,
    TotalChangeBytesExceeded = 110,
    ProposalTooLarge = 120
}

public sealed record ValidatedDeveloperChange(
    DeveloperChangeOperationType Operation,
    string RelativePath,
    string Intent,
    string Content,
    string ExpectedContentSha256,
    int ContentBytes);

public sealed record ValidatedDeveloperProposal(
    string Summary,
    IReadOnlyList<ValidatedDeveloperChange> Changes,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Risks,
    long TotalChangeBytes,
    long ProposalBytes);

public sealed record DeveloperProposalPolicyResult(
    ValidatedDeveloperProposal? Proposal,
    DeveloperProposalFailureKind FailureKind,
    string ErrorCode)
{
    public bool IsSuccess =>
        FailureKind ==
            DeveloperProposalFailureKind.None &&
        Proposal is not null &&
        string.IsNullOrEmpty(ErrorCode);

    public static DeveloperProposalPolicyResult Success(
        ValidatedDeveloperProposal proposal)
    {
        return new DeveloperProposalPolicyResult(
            proposal,
            DeveloperProposalFailureKind.None,
            string.Empty);
    }

    public static DeveloperProposalPolicyResult Failure(
        DeveloperProposalFailureKind failureKind,
        string errorCode)
    {
        return new DeveloperProposalPolicyResult(
            null,
            failureKind,
            errorCode);
    }
}

public interface IDeveloperProposalPolicy
{
    DeveloperProposalPolicyResult Validate(
        DeveloperProposal? proposal);
}
