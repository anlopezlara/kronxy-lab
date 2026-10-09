using System.Text;
using System.Text.Json;
using Kronxy.Web.Models;

namespace Kronxy.Web.Presentation;

public sealed record ArtifactDiagnosticPresentation
{
    public required string RawContent { get; init; }
    public string? NestedContent { get; init; }
    public bool NestedContentIsJson { get; init; }
    public string? Provider { get; init; }
    public string? LogicalModel { get; init; }
    public string? PhysicalModel { get; init; }
    public string? Duration { get; init; }
    public string? PromptTokens { get; init; }
    public string? CompletionTokens { get; init; }
    public string? TerminationReason { get; init; }
    public string? ErrorCode { get; init; }
    public string? SuccessStatus { get; init; }
    public IReadOnlyList<string> FilesToInspect { get; init; } = [];
    public IReadOnlyList<string> CandidateFilesToModify { get; init; } = [];
}

public static class ArtifactDiagnostics
{
    private static readonly string[] DiagnosticMarkers =
    [
        "RejectedResponse",
        "Failure",
        "Error",
        "Correction",
        "Recovery",
        "Validation",
        "Diagnostic"
    ];

    public static bool IsDiagnostic(string? artifactType)
    {
        if (string.IsNullOrWhiteSpace(artifactType) ||
            artifactType.EndsWith("Receipt", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return DiagnosticMarkers.Any(marker =>
            artifactType.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    public static ArtifactDto? Select(
        IEnumerable<ArtifactDto> artifacts,
        string? correlationId)
    {
        ArtifactDto[] candidates = artifacts
            .Where(item => IsDiagnostic(item.ArtifactType))
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.ArtifactId)
            .ToArray();

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            ArtifactDto? exact = candidates.FirstOrDefault(item =>
                string.Equals(
                    item.CorrelationId,
                    correlationId,
                    StringComparison.Ordinal));

            if (exact is not null)
            {
                return exact;
            }
        }

        return candidates.FirstOrDefault();
    }

    public static ArtifactDiagnosticPresentation Present(
        ArtifactContentDto artifact)
    {
        string text = Encoding.UTF8.GetString(artifact.Content);

        if (!artifact.ContentType.Contains(
                "json",
                StringComparison.OrdinalIgnoreCase))
        {
            return new ArtifactDiagnosticPresentation
            {
                RawContent = text
            };
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            JsonElement root = document.RootElement;
            string raw = Format(root);
            string? nested = Text(root, "Content");
            bool nestedIsJson = false;
            IReadOnlyList<string> filesToInspect = [];
            IReadOnlyList<string> candidateFilesToModify = [];

            if (!string.IsNullOrWhiteSpace(nested))
            {
                try
                {
                    using JsonDocument nestedDocument =
                        JsonDocument.Parse(nested);
                    nested = Format(nestedDocument.RootElement);
                    nestedIsJson = true;
                    filesToInspect = Strings(
                        nestedDocument.RootElement,
                        "filesToInspect");
                    candidateFilesToModify = Strings(
                        nestedDocument.RootElement,
                        "candidateFilesToModify");
                }
                catch (JsonException)
                {
                    // The provider may legitimately return plain text content.
                }
            }

            return new ArtifactDiagnosticPresentation
            {
                RawContent = raw,
                NestedContent = nested,
                NestedContentIsJson = nestedIsJson,
                Provider = Text(root, "Provider"),
                LogicalModel = Text(root, "LogicalModel"),
                PhysicalModel = Text(root, "PhysicalModel"),
                Duration = Scalar(root, "Duration"),
                PromptTokens = UsageScalar(root, "PromptTokens"),
                CompletionTokens = UsageScalar(root, "CompletionTokens"),
                TerminationReason = Scalar(root, "TerminationReason"),
                ErrorCode = NonEmpty(Text(root, "ErrorCode")),
                SuccessStatus = Success(root),
                FilesToInspect = filesToInspect,
                CandidateFilesToModify = candidateFilesToModify
            };
        }
        catch (JsonException)
        {
            return new ArtifactDiagnosticPresentation
            {
                RawContent = text
            };
        }
    }

    private static string Format(JsonElement element) =>
        JsonSerializer.Serialize(
            element,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

    private static string? Text(JsonElement element, string name) =>
        Property(element, name) is { ValueKind: JsonValueKind.String } value
            ? value.GetString()
            : null;

    private static string? Scalar(JsonElement element, string name)
    {
        JsonElement? value = Property(element, name);
        return value is null || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? null
            : value.Value.ToString();
    }

    private static string? UsageScalar(JsonElement element, string name)
    {
        JsonElement? usage = Property(element, "Usage");
        return usage is { ValueKind: JsonValueKind.Object }
            ? Scalar(usage.Value, name)
            : null;
    }

    private static string? Success(JsonElement element)
    {
        JsonElement? value = Property(element, "IsSuccess");
        return value is { ValueKind: JsonValueKind.True }
            ? "Success"
            : value is { ValueKind: JsonValueKind.False }
                ? "Failure"
                : null;
    }

    private static IReadOnlyList<string> Strings(
        JsonElement element,
        string name)
    {
        JsonElement? value = Property(element, name);
        if (value is not { ValueKind: JsonValueKind.Array })
        {
            return [];
        }

        return value.Value
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToArray();
    }

    private static JsonElement? Property(
        JsonElement element,
        string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(
                    property.Name,
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static string? NonEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
