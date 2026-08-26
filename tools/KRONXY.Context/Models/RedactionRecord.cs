using System.Text.Json.Serialization;

namespace Kronxy.Context.Models;

public sealed record RedactionRecord
{
    [JsonPropertyName("relativePath")]
    public string RelativePath { get; init; } = string.Empty;
    [JsonPropertyName("ruleId")]
    public string RuleId { get; init; } = string.Empty;
    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;
    [JsonPropertyName("lineNumber")]
    public int LineNumber { get; init; }
    [JsonPropertyName("matchCount")]
    public int MatchCount { get; init; }
}
