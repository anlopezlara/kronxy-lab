using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kronxy.Application.Execution;

[JsonConverter(
    typeof(
        JsonStringEnumConverter<
            DeveloperChangeOperationType>))]
public enum DeveloperChangeOperationType
{
    CreateFile = 10,
    ReplaceFile = 20
}

public sealed record DeveloperChangeOperation
{
    [JsonPropertyName("operation")]
    public required DeveloperChangeOperationType Operation
    {
        get;
        init;
    }

    [JsonPropertyName("relativePath")]
    public required string RelativePath { get; init; }

    [JsonPropertyName("intent")]
    public required string Intent { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }

    [JsonPropertyName("expectedContentSha256")]
    public required string ExpectedContentSha256
    {
        get;
        init;
    }
}

public sealed record DeveloperProposal
{
    [JsonPropertyName("summary")]
    public required string Summary { get; init; }

    [JsonPropertyName("changes")]
    public required IReadOnlyList<
        DeveloperChangeOperation> Changes
    {
        get;
        init;
    }

    [JsonPropertyName("assumptions")]
    public required IReadOnlyList<string> Assumptions
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
}

public static class DeveloperContractSchema
{
    private const string Schema =
        """
        {
          "type": "object",
          "properties": {
            "summary": {
              "type": "string",
              "maxLength": 80
            },
            "changes": {
              "type": "array",
              "minItems": 1,
              "maxItems": 4,
              "items": {
                "type": "object",
                "properties": {
                  "operation": {
                    "type": "string",
                    "enum": [
                      "CreateFile",
                      "ReplaceFile"
                    ]
                  },
                  "relativePath": {
                    "type": "string"
                  },
                  "intent": {
                    "type": "string",
                    "maxLength": 96
                  },
                  "content": {
                    "type": "string",
                    "maxLength": 4000
                  },
                  "expectedContentSha256": {
                    "type": "string"
                  }
                },
                "required": [
                  "operation",
                  "relativePath",
                  "intent",
                  "content",
                  "expectedContentSha256"
                ],
                "additionalProperties": false
              }
            },
            "assumptions": {
              "type": "array",
              "maxItems": 1,
              "items": {
                "type": "string",
                "maxLength": 120
              }
            },
            "risks": {
              "type": "array",
              "maxItems": 1,
              "items": {
                "type": "string",
                "maxLength": 120
              }
            }
          },
          "required": [
            "summary",
            "changes",
            "assumptions",
            "risks"
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
