using Kronxy.Application.Execution;
using Kronxy.Infrastructure.Execution;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class PlannerPlanPolicyTests
{
    private readonly PlannerPlanPolicy policy =
        new();

    [Fact]
    public void Matching_objective_is_accepted()
    {
        const string request =
            "Harden external KRX identifier validation.";

        PlannerPlanPolicyResult result =
            policy.Validate(
                ValidPlan(request),
                request);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            string.Empty,
            result.ErrorCode);
    }

    [Fact]
    public void Unrelated_objective_is_rejected()
    {
        const string request =
            "Harden external KRX identifier validation.";

        PlannerPlan plan =
            ValidPlan(
                "Create a new project.");

        PlannerPlanPolicyResult result =
            policy.Validate(
                plan,
                request);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "PLANNING_OBJECTIVE_MISMATCH",
            result.ErrorCode);
    }

    [Fact]
    public void Grounded_paraphrased_objective_is_accepted()
    {
        const string request =
            "Reject lowercase KRX prefixes while preserving canonical KRX identifiers.";

        PlannerPlanPolicyResult result =
            policy.Validate(
                ValidPlan(
                    "Harden KRX prefix validation and preserve canonical identifiers."),
                request);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Unsafe_path_is_rejected()
    {
        const string request =
            "Implement safely.";

        PlannerPlan plan =
            ValidPlan(request) with
            {
                CandidateFilesToModify =
                [
                    "../escape.cs"
                ]
            };

        PlannerPlanPolicyResult result =
            policy.Validate(
                plan,
                request);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "PLANNING_PATH_INVALID",
            result.ErrorCode);
    }

    [Fact]
    public void Duplicate_path_is_rejected()
    {
        const string request =
            "Implement safely.";

        PlannerPlan plan =
            ValidPlan(request) with
            {
                FilesToInspect =
                [
                    "src/a.cs",
                    "src/a.cs"
                ]
            };

        PlannerPlanPolicyResult result =
            policy.Validate(
                plan,
                request);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "PLANNING_PATH_DUPLICATE",
            result.ErrorCode);
    }

    private static PlannerPlan ValidPlan(
        string objective) =>
        new()
        {
            Objective = objective,
            FilesToInspect =
            [
                "src/a.cs"
            ],
            CandidateFilesToModify =
            [
                "src/a.cs"
            ],
            Strategy =
                "Apply the smallest safe change.",
            AcceptanceCriteria =
            [
                "Behavior is covered by tests."
            ],
            Risks = [],
            ExpectedTests =
            [
                "Run focused tests."
            ],
            Assumptions = [],
            Uncertainties = []
        };
}
