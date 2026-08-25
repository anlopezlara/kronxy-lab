using System.Text.Json.Serialization;

namespace Kronxy.Context.Models;

public sealed record ManifestFileEntry
{
    [JsonPropertyName("relativePath")]
    public string RelativePath { get; init; } = string.Empty;
    [JsonPropertyName("function")]
    public string Function { get; init; } = string.Empty;
    [JsonPropertyName("inclusionReason")]
    public string InclusionReason { get; init; } = string.Empty;
    [JsonPropertyName("gitStatus")]
    public string GitStatus { get; init; } = string.Empty;
    [JsonPropertyName("originalSize")]
    public long OriginalSize { get; init; }
    [JsonPropertyName("packagedSize")]
    public long PackagedSize { get; init; }
    [JsonPropertyName("packagedSha256")]
    public string PackagedSha256 { get; init; } = string.Empty;
    [JsonPropertyName("isTruncated")]
    public bool IsTruncated { get; init; }
    [JsonPropertyName("isSanitized")]
    public bool IsSanitized { get; init; }
    [JsonPropertyName("encoding")]
    public string? Encoding { get; init; }
    [JsonPropertyName("isBinary")]
    public bool IsBinary { get; init; }
    [JsonPropertyName("previousPath")]
    public string? PreviousPath { get; init; }
}
