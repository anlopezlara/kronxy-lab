using System.Text.Json;
using Kronxy.Application.Execution;
using Kronxy.Infrastructure.AI;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class PlannerContractTests
{
    private readonly AiStructuredOutputValidator validator =
        new();

    [Fact]
    public void Valid_plan_matches_schema_and_deserializes()
    {
        const string json =
            """
            {
              "objective": "Add a deterministic helper.",
              "filesToInspect": [
                "src/Kronxy.Application/Example.cs"
              ],
              "candidateFilesToModify": [
                "src/Kronxy.Application/Example.cs"
              ],
              "strategy": "Inspect, modify and test.",
              "acceptanceCriteria": [
                "The solution builds."
              ],
              "risks": [
                "Unexpected callers."
              ],
              "expectedTests": [
                "Run the applicable unit tests."
              ],
              "assumptions": [],
              "uncertainties": []
            }
            """;

        bool valid =
            validator.TryValidate(
                PlannerContractSchema.CreateSchema(),
                json,
                out string error);

        PlannerPlan? plan =
            JsonSerializer.Deserialize<PlannerPlan>(
                json);

        Assert.True(valid);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(plan);
        Assert.Equal(
            "Add a deterministic helper.",
            plan.Objective);
        Assert.Single(plan.FilesToInspect);
        Assert.Single(plan.CandidateFilesToModify);
    }

    [Fact]
    public void Missing_required_section_is_rejected()
    {
        const string json =
            """
            {
              "objective": "Incomplete plan."
            }
            """;

        bool valid =
            validator.TryValidate(
                PlannerContractSchema.CreateSchema(),
                json,
                out string error);

        Assert.False(valid);
        Assert.Equal(
            "AI_STRUCTURED_SCHEMA_MISMATCH",
            error);
    }

    [Fact]
    public void Additional_operational_property_is_rejected()
    {
        const string json =
            """
            {
              "objective": "Unsafe plan.",
              "filesToInspect": [],
              "candidateFilesToModify": [],
              "strategy": "Do not execute.",
              "acceptanceCriteria": [],
              "risks": [],
              "expectedTests": [],
              "assumptions": [],
              "uncertainties": [],
              "command": "rm -rf /"
            }
            """;

        bool valid =
            validator.TryValidate(
                PlannerContractSchema.CreateSchema(),
                json,
                out string error);

        Assert.False(valid);
        Assert.Equal(
            "AI_STRUCTURED_SCHEMA_MISMATCH",
            error);
    }
}
