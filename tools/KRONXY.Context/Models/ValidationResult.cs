using System.Text.Json.Serialization;

namespace Kronxy.Context.Models;

public sealed record ValidationResult
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
    [JsonPropertyName("isSuccessful")]
    public bool IsSuccessful { get; init; }
    [JsonPropertyName("exitCode")]
    public int? ExitCode { get; init; }
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}
