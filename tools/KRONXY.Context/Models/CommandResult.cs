using System.Text.Json.Serialization;

namespace Kronxy.Context.Models;

public sealed record CommandResult
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
    [JsonPropertyName("exitCode")]
    public int ExitCode { get; init; }
    [JsonPropertyName("isSuccessful")]
    public bool IsSuccessful { get; init; }
    [JsonPropertyName("timedOut")]
    public bool TimedOut { get; init; }
    [JsonPropertyName("sanitizedOutput")]
    public string SanitizedOutput { get; init; } = string.Empty;
    [JsonPropertyName("sanitizedError")]
    public string SanitizedError { get; init; } = string.Empty;
    [JsonPropertyName("duration")]
    public TimeSpan Duration { get; init; }
}
