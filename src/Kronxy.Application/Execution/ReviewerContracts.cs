using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kronxy.Application.Execution;

[JsonConverter(typeof(JsonStringEnumConverter<ReviewerDecision>))]
public enum ReviewerDecision
{
    Approved = 10,
    ChangesRequired = 20,
    Rejected = 30
}

public sealed record ReviewerFinding
{
    [JsonPropertyName("category")] public required string Category { get; init; }
    [JsonPropertyName("description")] public required string Description { get; init; }
}

public sealed record ReviewerCorrection
{
    [JsonPropertyName("relativePath")] public required string RelativePath { get; init; }
    [JsonPropertyName("instruction")] public required string Instruction { get; init; }
}

public sealed record ReviewerReview
{
    [JsonPropertyName("decision")] public required ReviewerDecision Decision { get; init; }
    [JsonPropertyName("findings")] public required IReadOnlyList<ReviewerFinding> Findings { get; init; }
    [JsonPropertyName("requiredCorrections")] public required IReadOnlyList<ReviewerCorrection> RequiredCorrections { get; init; }
    [JsonPropertyName("riskAssessment")] public required string RiskAssessment { get; init; }
    [JsonPropertyName("summary")] public required string Summary { get; init; }
    [JsonPropertyName("deterministicAcceptanceGate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DeterministicAcceptanceGateResult? DeterministicAcceptanceGate { get; init; }
    [JsonPropertyName("reviewerContradictsDeterministicEvidence")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ReviewerContradictsDeterministicEvidence { get; init; }
}

public static class ReviewerContractSchema
{
    private const string Schema = """
    {
      "type":"object",
      "properties":{
        "decision":{"type":"string","enum":["Approved","ChangesRequired","Rejected"]},
        "findings":{"type":"array","items":{"type":"object","properties":{"category":{"type":"string"},"description":{"type":"string"}},"required":["category","description"],"additionalProperties":false}},
        "requiredCorrections":{"type":"array","items":{"type":"object","properties":{"relativePath":{"type":"string"},"instruction":{"type":"string"}},"required":["relativePath","instruction"],"additionalProperties":false}},
        "riskAssessment":{"type":"string"},
        "summary":{"type":"string"}
      },
      "required":["decision","findings","requiredCorrections","riskAssessment","summary"],
      "additionalProperties":false
    }
    """;

    public static JsonElement CreateSchema()
    {
        using JsonDocument document = JsonDocument.Parse(Schema);
        return document.RootElement.Clone();
    }
}
