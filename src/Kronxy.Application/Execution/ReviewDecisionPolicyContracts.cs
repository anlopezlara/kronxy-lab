namespace Kronxy.Application.Execution;

public enum ReviewDecisionFailureKind { None=0, InvalidEvidence=10 }

public sealed record ReviewDecisionInput(
    Guid JobId, Guid RunId, ReviewerReview Review,
    BuildExecutionReport BuildReport, TestExecutionReport TestReport,
    ObservedChangeManifest ObservedChanges);

public sealed record ReviewDecisionResult(
    ReviewerDecision? Decision, ReviewDecisionFailureKind FailureKind, string ErrorCode)
{
    public bool IsSuccess => FailureKind == ReviewDecisionFailureKind.None && Decision.HasValue;
    public static ReviewDecisionResult Success(ReviewerDecision decision) =>
        new(decision, ReviewDecisionFailureKind.None, string.Empty);
    public static ReviewDecisionResult Failure(string code) =>
        new(null, ReviewDecisionFailureKind.InvalidEvidence, code);
}

public interface IReviewDecisionPolicy
{
    ReviewDecisionResult Evaluate(ReviewDecisionInput input);
}
