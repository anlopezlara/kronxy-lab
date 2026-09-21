using System.Text.Json;
using Kronxy.Infrastructure.AI;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class AiStructuredOutputValidatorTests
{
    private readonly AiStructuredOutputValidator validator =
        new();

    [Fact]
    public void Valid_object_is_accepted()
    {
        JsonElement schema =
            Parse(
                """
                {
                  "type": "object",
                  "properties": {
                    "status": {
                      "type": "string",
                      "const": "ok"
                    },
                    "value": {
                      "type": "integer"
                    }
                  },
                  "required": [
                    "status",
                    "value"
                  ],
                  "additionalProperties": false
                }
                """);

        bool valid =
            validator.TryValidate(
                schema,
                """
                {
                  "status": "ok",
                  "value": 42
                }
                """,
                out string error);

        Assert.True(valid);
        Assert.Equal(
            string.Empty,
            error);
    }

    [Fact]
    public void Invalid_json_is_rejected()
    {
        bool valid =
            validator.TryValidate(
                SimpleSchema(),
                "{broken",
                out string error);

        Assert.False(valid);
        Assert.Equal(
            "AI_STRUCTURED_INVALID_JSON",
            error);
    }

    [Fact]
    public void Missing_required_property_is_rejected()
    {
        bool valid =
            validator.TryValidate(
                SimpleSchema(),
                """
                {
                  "status": "ok"
                }
                """,
                out string error);

        Assert.False(valid);
        Assert.Equal(
            "AI_STRUCTURED_SCHEMA_MISMATCH",
            error);
    }

    [Fact]
    public void Additional_property_is_rejected()
    {
        bool valid =
            validator.TryValidate(
                SimpleSchema(),
                """
                {
                  "status": "ok",
                  "value": 42,
                  "command": "rm -rf /"
                }
                """,
                out string error);

        Assert.False(valid);
        Assert.Equal(
            "AI_STRUCTURED_SCHEMA_MISMATCH",
            error);
    }

    [Fact]
    public void Wrong_const_is_rejected()
    {
        bool valid =
            validator.TryValidate(
                SimpleSchema(),
                """
                {
                  "status": "fail",
                  "value": 42
                }
                """,
                out string error);

        Assert.False(valid);
        Assert.Equal(
            "AI_STRUCTURED_SCHEMA_MISMATCH",
            error);
    }

    [Fact]
    public void Array_items_are_validated()
    {
        JsonElement schema =
            Parse(
                """
                {
                  "type": "array",
                  "items": {
                    "type": "string"
                  }
                }
                """);

        bool valid =
            validator.TryValidate(
                schema,
                """
                [
                  "first",
                  "second"
                ]
                """,
                out string error);

        Assert.True(valid);
        Assert.Equal(
            string.Empty,
            error);
    }

    [Fact]
    public void Array_item_with_wrong_type_is_rejected()
    {
        JsonElement schema =
            Parse(
                """
                {
                  "type": "array",
                  "items": {
                    "type": "string"
                  }
                }
                """);

        bool valid =
            validator.TryValidate(
                schema,
                """
                [
                  "first",
                  42
                ]
                """,
                out string error);

        Assert.False(valid);
        Assert.Equal(
            "AI_STRUCTURED_SCHEMA_MISMATCH",
            error);
    }

    [Fact]
    public void Invalid_items_schema_is_rejected()
    {
        JsonElement schema =
            Parse(
                """
                {
                  "type": "array",
                  "items": "string"
                }
                """);

        bool valid =
            validator.TryValidate(
                schema,
                "[]",
                out string error);

        Assert.False(valid);
        Assert.Equal(
            "AI_STRUCTURED_SCHEMA_UNSUPPORTED",
            error);
    }

    [Fact]
    public void Unsupported_schema_keyword_is_rejected()
    {
        JsonElement schema =
            Parse(
                """
                {
                  "type": "object",
                  "minProperties": 1
                }
                """);

        bool valid =
            validator.TryValidate(
                schema,
                "{}",
                out string error);

        Assert.False(valid);
        Assert.Equal(
            "AI_STRUCTURED_SCHEMA_UNSUPPORTED",
            error);
    }

    private static JsonElement SimpleSchema()
    {
        return Parse(
            """
            {
              "type": "object",
              "properties": {
                "status": {
                  "type": "string",
                  "const": "ok"
                },
                "value": {
                  "type": "integer"
                }
              },
              "required": [
                "status",
                "value"
              ],
              "additionalProperties": false
            }
            """);
    }

    private static JsonElement Parse(
        string json)
    {
        return JsonSerializer
            .Deserialize<JsonElement>(
                json);
    }
}
