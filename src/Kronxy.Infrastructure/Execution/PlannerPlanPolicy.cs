using System.Diagnostics.CodeAnalysis;
using Kronxy.Application.Execution;

namespace Kronxy.Infrastructure.Execution;

public interface IPlannerPlanPolicy
{
    PlannerPlanPolicyResult Validate(
        PlannerPlan? plan,
        string jobRequest);
}

public sealed record PlannerPlanPolicyResult(
    bool IsSuccess,
    string ErrorCode)
{
    public static PlannerPlanPolicyResult Success() =>
        new(
            true,
            string.Empty);

    public static PlannerPlanPolicyResult Failure(
        string errorCode) =>
        new(
            false,
            errorCode);
}

public sealed class PlannerPlanPolicy :
    IPlannerPlanPolicy
{
    public PlannerPlanPolicyResult Validate(
        PlannerPlan? plan,
        string jobRequest)
    {
        if (!HasValidShape(
                plan) ||
            string.IsNullOrWhiteSpace(
                jobRequest))
        {
            return Failure(
                "PLANNING_PLAN_INVALID");
        }

        if (!IsObjectiveGrounded(
                plan.Objective,
                jobRequest))
        {
            return Failure(
                "PLANNING_OBJECTIVE_MISMATCH");
        }

        if (!ValidPaths(
                plan.FilesToInspect) ||
            !ValidPaths(
                plan.CandidateFilesToModify))
        {
            return Failure(
                "PLANNING_PATH_INVALID");
        }

        if (HasDuplicates(
                plan.FilesToInspect) ||
            HasDuplicates(
                plan.CandidateFilesToModify))
        {
            return Failure(
                "PLANNING_PATH_DUPLICATE");
        }

        return PlannerPlanPolicyResult.Success();
    }

    private static bool HasValidShape(
        [NotNullWhen(true)]
        PlannerPlan? plan) =>
        plan is not null &&
        !string.IsNullOrWhiteSpace(
            plan.Objective) &&
        plan.FilesToInspect is not null &&
        plan.CandidateFilesToModify is not null &&
        !string.IsNullOrWhiteSpace(
            plan.Strategy) &&
        plan.AcceptanceCriteria is not null &&
        plan.Risks is not null &&
        plan.ExpectedTests is not null &&
        plan.Assumptions is not null &&
        plan.Uncertainties is not null;

    private static bool IsObjectiveGrounded(
        string objective,
        string jobRequest)
    {
        HashSet<string> requestTokens =
            PlanningPriorityPathSelector.Tokenize(
                jobRequest);

        HashSet<string> objectiveTokens =
            PlanningPriorityPathSelector.Tokenize(
                objective);

        if (requestTokens.Count == 0 ||
            objectiveTokens.Count == 0)
        {
            return false;
        }

        int shared =
            requestTokens.Count(
                objectiveTokens.Contains);

        return shared >= 2;
    }

    private static bool ValidPaths(
        IReadOnlyList<string> paths) =>
        paths.All(
            IsValidRelativePath);

    private static bool HasDuplicates(
        IReadOnlyList<string> paths)
    {
        var unique =
            new HashSet<string>(
                StringComparer.Ordinal);

        return paths.Any(
            path =>
                !unique.Add(path));
    }

    private static bool IsValidRelativePath(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value) ||
            !string.Equals(
                value,
                value.Trim(),
                StringComparison.Ordinal) ||
            value.StartsWith(
                "/",
                StringComparison.Ordinal) ||
            value.Contains('\\') ||
            value.IndexOfAny(
                [
                    '\0',
                    '\r',
                    '\n'
                ]) >= 0 ||
            Path.IsPathFullyQualified(
                value))
        {
            return false;
        }

        return value
            .Split('/')
            .All(
                segment =>
                    segment.Length > 0 &&
                    segment is not "." and not "..");
    }

    private static PlannerPlanPolicyResult Failure(
        string errorCode) =>
        PlannerPlanPolicyResult.Failure(
            errorCode);
}
