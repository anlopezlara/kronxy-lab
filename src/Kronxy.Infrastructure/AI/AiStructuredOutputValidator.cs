using System.Text.Json;

namespace Kronxy.Infrastructure.AI;

public sealed class AiStructuredOutputValidator
{
    private static readonly HashSet<string>
        SupportedKeywords =
        new(
            [
                "type",
                "properties",
                "required",
                "additionalProperties",
                "const",
                "enum"
            ],
            StringComparer.Ordinal);

    public bool TryValidate(
        JsonElement schema,
        string content,
        out string errorCode)
    {
        errorCode =
            string.Empty;

        JsonDocument document;

        try
        {
            document =
                JsonDocument.Parse(
                    content);
        }
        catch (JsonException)
        {
            errorCode =
                "AI_STRUCTURED_INVALID_JSON";

            return false;
        }

        using (document)
        {
            if (!ValidateSchemaDefinition(
                    schema))
            {
                errorCode =
                    "AI_STRUCTURED_SCHEMA_UNSUPPORTED";

                return false;
            }

            if (!ValidateElement(
                    schema,
                    document.RootElement))
            {
                errorCode =
                    "AI_STRUCTURED_SCHEMA_MISMATCH";

                return false;
            }

            return true;
        }
    }

    private static bool ValidateSchemaDefinition(
        JsonElement schema)
    {
        if (schema.ValueKind !=
            JsonValueKind.Object)
        {
            return false;
        }

        foreach (
            JsonProperty property
            in schema.EnumerateObject())
        {
            if (!SupportedKeywords.Contains(
                    property.Name))
            {
                return false;
            }
        }

        if (schema.TryGetProperty(
                "type",
                out JsonElement typeElement))
        {
            if (typeElement.ValueKind !=
                JsonValueKind.String)
            {
                return false;
            }

            string? type =
                typeElement.GetString();

            if (type is not (
                "object" or
                "array" or
                "string" or
                "number" or
                "integer" or
                "boolean" or
                "null"))
            {
                return false;
            }
        }

        if (schema.TryGetProperty(
                "properties",
                out JsonElement properties))
        {
            if (properties.ValueKind !=
                JsonValueKind.Object)
            {
                return false;
            }

            foreach (
                JsonProperty property
                in properties.EnumerateObject())
            {
                if (!ValidateSchemaDefinition(
                        property.Value))
                {
                    return false;
                }
            }
        }

        if (schema.TryGetProperty(
                "required",
                out JsonElement required))
        {
            if (required.ValueKind !=
                JsonValueKind.Array)
            {
                return false;
            }

            foreach (
                JsonElement element
                in required.EnumerateArray())
            {
                if (element.ValueKind !=
                    JsonValueKind.String)
                {
                    return false;
                }
            }
        }

        if (schema.TryGetProperty(
                "additionalProperties",
                out JsonElement additional))
        {
            if (additional.ValueKind is not (
                JsonValueKind.True or
                JsonValueKind.False))
            {
                return false;
            }
        }

        if (schema.TryGetProperty(
                "enum",
                out JsonElement enumElement) &&
            enumElement.ValueKind !=
                JsonValueKind.Array)
        {
            return false;
        }

        return true;
    }

    private static bool ValidateElement(
        JsonElement schema,
        JsonElement value)
    {
        if (schema.TryGetProperty(
                "type",
                out JsonElement typeElement))
        {
            if (!MatchesType(
                    typeElement.GetString(),
                    value))
            {
                return false;
            }
        }

        if (schema.TryGetProperty(
                "const",
                out JsonElement constElement))
        {
            if (!JsonEquals(
                    constElement,
                    value))
            {
                return false;
            }
        }

        if (schema.TryGetProperty(
                "enum",
                out JsonElement enumElement))
        {
            bool matched =
                enumElement
                    .EnumerateArray()
                    .Any(
                        candidate =>
                            JsonEquals(
                                candidate,
                                value));

            if (!matched)
            {
                return false;
            }
        }

        if (value.ValueKind ==
            JsonValueKind.Object)
        {
            if (!ValidateObject(
                    schema,
                    value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateObject(
        JsonElement schema,
        JsonElement value)
    {
        var properties =
            new Dictionary<
                string,
                JsonElement>(
                StringComparer.Ordinal);

        if (schema.TryGetProperty(
                "properties",
                out JsonElement
                    propertySchemas))
        {
            foreach (
                JsonProperty property
                in propertySchemas
                    .EnumerateObject())
            {
                properties[
                    property.Name] =
                    property.Value;
            }
        }

        if (schema.TryGetProperty(
                "required",
                out JsonElement required))
        {
            foreach (
                JsonElement requiredName
                in required.EnumerateArray())
            {
                string? name =
                    requiredName.GetString();

                if (name is null ||
                    !value.TryGetProperty(
                        name,
                        out _))
                {
                    return false;
                }
            }
        }

        bool allowAdditional =
            true;

        if (schema.TryGetProperty(
                "additionalProperties",
                out JsonElement additional))
        {
            allowAdditional =
                additional.GetBoolean();
        }

        foreach (
            JsonProperty property
            in value.EnumerateObject())
        {
            if (properties.TryGetValue(
                    property.Name,
                    out JsonElement
                        propertySchema))
            {
                if (!ValidateElement(
                        propertySchema,
                        property.Value))
                {
                    return false;
                }

                continue;
            }

            if (!allowAdditional)
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesType(
        string? type,
        JsonElement value)
    {
        return type switch
        {
            null =>
                true,

            "object" =>
                value.ValueKind ==
                    JsonValueKind.Object,

            "array" =>
                value.ValueKind ==
                    JsonValueKind.Array,

            "string" =>
                value.ValueKind ==
                    JsonValueKind.String,

            "number" =>
                value.ValueKind ==
                    JsonValueKind.Number,

            "integer" =>
                value.ValueKind ==
                    JsonValueKind.Number &&
                value.TryGetInt64(
                    out _),

            "boolean" =>
                value.ValueKind is
                    JsonValueKind.True or
                    JsonValueKind.False,

            "null" =>
                value.ValueKind ==
                    JsonValueKind.Null,

            _ =>
                false
        };
    }

    private static bool JsonEquals(
        JsonElement left,
        JsonElement right)
    {
        return left.ValueKind ==
                right.ValueKind &&
            string.Equals(
                left.GetRawText(),
                right.GetRawText(),
                StringComparison.Ordinal);
    }
}
