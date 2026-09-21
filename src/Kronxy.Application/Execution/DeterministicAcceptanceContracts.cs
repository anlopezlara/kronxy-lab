namespace Kronxy.Application.Execution;

public enum DeterministicCriterionKind
{
    FileExists = 10,
    CSharpPropertyExists = 20,
    CSharpMethodExists = 30,
    StringLiteralExists = 40
}

public enum DeterministicCriterionStatus
{
    Pass = 10,
    Fail = 20,
    Unevaluable = 30
}

public sealed record DeterministicAcceptanceCriterion(
    string Id,
    DeterministicCriterionKind Kind,
    string RelativePath,
    string Expected,
    string SourceRequirement);

public sealed record DeterministicAcceptanceCriterionResult(
    DeterministicAcceptanceCriterion Criterion,
    DeterministicCriterionStatus Status,
    string Evidence);

public sealed record DeterministicAcceptanceGateResult(
    IReadOnlyList<DeterministicAcceptanceCriterionResult> Criteria,
    IReadOnlyList<string> SemanticCriteria)
{
    public bool IsSuccess =>
        Criteria.Count > 0 &&
        Criteria.All(result => result.Status == DeterministicCriterionStatus.Pass);
}

public interface IDeterministicAcceptanceGate
{
    DeterministicAcceptanceGateResult Evaluate(
        string jobRequest,
        PlannerPlan plan,
        ReviewerEffectiveSourceSnapshot snapshot);
}
