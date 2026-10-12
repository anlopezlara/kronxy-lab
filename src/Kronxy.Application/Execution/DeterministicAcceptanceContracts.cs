namespace Kronxy.Application.Execution;

public enum DeterministicCriterionKind
{
    FileExists = 10,
    CSharpPropertyExists = 20,
    CSharpMethodExists = 30,
    StringLiteralExists = 40,
    PassingTest = 50,
    SemanticRequirement = 60
}

public enum DeterministicCriterionStatus
{
    Passed = 10,
    Pass = Passed,
    Failed = 20,
    Fail = Failed,
    NotProven = 30,
    Unevaluable = NotProven,
    NotApplicable = 40
}

public sealed record DeterministicAcceptanceCriterion(
    string Id,
    DeterministicCriterionKind Kind,
    string RelativePath,
    string Expected,
    string SourceRequirement)
{
    public string Requirement => SourceRequirement;
    public string VerificationMethod => Kind.ToString();
}

public sealed record DeterministicAcceptanceCriterionResult(
    DeterministicAcceptanceCriterion Criterion,
    DeterministicCriterionStatus Status,
    string Evidence)
{
    public string Actual { get; init; } = string.Empty;
}

public sealed record DeterministicAcceptanceGateResult(
    IReadOnlyList<DeterministicAcceptanceCriterionResult> Criteria,
    IReadOnlyList<string> SemanticCriteria)
{
    public bool IsSuccess =>
        Criteria.Count > 0 &&
        Criteria.All(result => result.Status is
            DeterministicCriterionStatus.Passed or
            DeterministicCriterionStatus.NotApplicable);
}

public interface IDeterministicAcceptanceGate
{
    DeterministicAcceptanceGateResult Evaluate(
        string jobRequest,
        PlannerPlan plan,
        ReviewerEffectiveSourceSnapshot snapshot,
        TestExecutionReport testReport);
}
