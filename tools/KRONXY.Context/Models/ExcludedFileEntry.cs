using System.Text.Json.Serialization;

namespace Kronxy.Context.Models;

public sealed record ExcludedFileEntry
{
    [JsonPropertyName("relativePath")]
    public string RelativePath { get; init; } = string.Empty;
    [JsonPropertyName("rule")]
    public string Rule { get; init; } = string.Empty;
    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;
    [JsonPropertyName("size")]
    public long? Size { get; init; }
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;
}
