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
    ReplaceFile = 20,
    LocalizedPatch = 30
}

public sealed record DeveloperLocalizedEdit
{
    [JsonPropertyName("oldText")]
    public required string OldText { get; init; }

    [JsonPropertyName("newText")]
    public required string NewText { get; init; }
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

    [JsonPropertyName("edits")]
    public IReadOnlyList<DeveloperLocalizedEdit> Edits { get; init; } = [];

    [JsonPropertyName("resultContentSha256")]
    public string ResultContentSha256 { get; init; } = string.Empty;
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
                      "ReplaceFile",
                      "LocalizedPatch"
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
                    "maxLength": 262144
                  },
                  "expectedContentSha256": {
                    "type": "string"
                  },
                  "edits": {
                    "type": "array",
                    "maxItems": 8,
                    "items": {
                      "type": "object",
                      "properties": {
                        "oldText": { "type": "string", "maxLength": 16384 },
                        "newText": { "type": "string", "maxLength": 16384 }
                      },
                      "required": ["oldText", "newText"],
                      "additionalProperties": false
                    }
                  },
                  "resultContentSha256": {
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
