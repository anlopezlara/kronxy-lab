using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kronxy.Application.Execution;

public sealed record PlannerAcceptanceTest
{
    [JsonPropertyName("requirement")]
    public required string Requirement { get; init; }

    [JsonPropertyName("testName")]
    public required string TestName { get; init; }
}

public sealed record PlannerPlan
{
    [JsonPropertyName("objective")]
    public required string Objective { get; init; }

    [JsonPropertyName("filesToInspect")]
    public required IReadOnlyList<string> FilesToInspect
    {
        get;
        init;
    }

    [JsonPropertyName("candidateFilesToModify")]
    public required IReadOnlyList<string>
        CandidateFilesToModify
    {
        get;
        init;
    }

    [JsonPropertyName("strategy")]
    public required string Strategy { get; init; }

    [JsonPropertyName("acceptanceCriteria")]
    public required IReadOnlyList<string>
        AcceptanceCriteria
    {
        get;
        init;
    }

    [JsonPropertyName("risks")]
    public required IReadOnlyList<string> Risks
    {
        get;
        init;
    }

    [JsonPropertyName("expectedTests")]
    public required IReadOnlyList<string> ExpectedTests
    {
        get;
        init;
    }

    [JsonPropertyName("acceptanceTestMappings")]
    public IReadOnlyList<PlannerAcceptanceTest> AcceptanceTestMappings
    {
        get;
        init;
    } = [];

    [JsonPropertyName("assumptions")]
    public required IReadOnlyList<string> Assumptions
    {
        get;
        init;
    }

    [JsonPropertyName("uncertainties")]
    public required IReadOnlyList<string> Uncertainties
    {
        get;
        init;
    }
}

public static class PlannerContractSchema
{
    private const string Schema =
        """
        {
          "type": "object",
          "properties": {
            "objective": {
              "type": "string"
            },
            "filesToInspect": {
              "type": "array",
              "items": {
                "type": "string"
              }
            },
            "candidateFilesToModify": {
              "type": "array",
              "items": {
                "type": "string"
              }
            },
            "strategy": {
              "type": "string"
            },
            "acceptanceCriteria": {
              "type": "array",
              "items": {
                "type": "string"
              }
            },
            "risks": {
              "type": "array",
              "items": {
                "type": "string"
              }
            },
            "expectedTests": {
              "type": "array",
              "items": {
                "type": "string"
              }
            },
            "acceptanceTestMappings": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "requirement": { "type": "string" },
                  "testName": { "type": "string" }
                },
                "required": ["requirement", "testName"],
                "additionalProperties": false
              }
            },
            "assumptions": {
              "type": "array",
              "items": {
                "type": "string"
              }
            },
            "uncertainties": {
              "type": "array",
              "items": {
                "type": "string"
              }
            }
          },
          "required": [
            "objective",
            "filesToInspect",
            "candidateFilesToModify",
            "strategy",
            "acceptanceCriteria",
            "risks",
            "expectedTests",
            "acceptanceTestMappings",
            "assumptions",
            "uncertainties"
          ],
          "additionalProperties": false
        }
        """;

    public static JsonElement CreateSchema()
    {
        using JsonDocument document =
            JsonDocument.Parse(Schema);

        return document.RootElement.Clone();
    }
}
