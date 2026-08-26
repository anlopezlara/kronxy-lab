using Kronxy.Context.Models;
using System.Text.Json.Serialization;

namespace Kronxy.Context.Redaction;

public enum RedactionCategory
{
    Password, Token, ApiKey, PrivateKey, ConnectionString, AuthorizationHeader,
    UrlCredential, SignedUrl, CloudCredential, CertificateSecret, GenericSecret
}

public enum RedactionStatus
{
    SuccessUnchanged, SuccessRedacted, InputLimitExceeded, TooManyRedactions,
    OutputLimitExceeded, RuleTimeout, SensitiveContentRemaining, UnprocessableFormat, InternalError
}

public enum RedactionSeverity { Warning, Error }

public sealed record RedactionFinding
{
    public string LogicalPath { get; init; } = string.Empty;
    public required string RuleId { get; init; }
    public required RedactionCategory Category { get; init; }
    public required int LineNumber { get; init; }
    public required RedactionSeverity Severity { get; init; }
    public required string Description { get; init; }
}

public sealed record RedactionLimits
{
    public int MaxInputBytes { get; init; } = 262_144;
    public int MaxOutputBytes { get; init; } = 524_288;
    public int MaxRedactionsPerFile { get; init; } = 1_000;
    public int MaxFindingsPerFile { get; init; } = 1_000;
    public TimeSpan RegexTimeout { get; init; } = TimeSpan.FromMilliseconds(250);

    internal bool IsValid() => MaxInputBytes > 0 && MaxOutputBytes > 0 &&
        MaxRedactionsPerFile > 0 && MaxFindingsPerFile > 0 &&
        RegexTimeout > TimeSpan.Zero && RegexTimeout <= TimeSpan.FromSeconds(10);
}

public sealed class RedactionRequest
{
    [JsonIgnore]
    public string LogicalPath { get; init; } = string.Empty;
    [JsonIgnore]
    public string Content { get; init; } = string.Empty;
    public RedactionLimits Limits { get; init; } = new();
    public override string ToString() => "RedactionRequest { LogicalPath = ***REDACTED***, Content = ***REDACTED*** }";
}

public sealed record RedactionResult
{
    public required RedactionStatus Status { get; init; }
    public string? Content { get; init; }
    public IReadOnlyList<RedactionRecord> Records { get; init; } = [];
    public IReadOnlyList<RedactionFinding> Findings { get; init; } = [];
    public bool IsSuccess => Status is RedactionStatus.SuccessUnchanged or RedactionStatus.SuccessRedacted;
    public override string ToString() => $"RedactionResult {{ Status = {Status}, Content = ***REDACTED*** }}";
}

public sealed record RedactionValidationResult
{
    public required bool IsSafe { get; init; }
    public IReadOnlyList<RedactionFinding> Findings { get; init; } = [];
}

public interface IContentRedactor
{
    RedactionResult Redact(RedactionRequest request);
}

public interface IRedactionValidator
{
    RedactionValidationResult Validate(string logicalPath, string redactedContent, RedactionLimits limits);
}

internal sealed record RedactionMatch(
    int Start, int Length, string RuleId, RedactionCategory Category, string Replacement, int Priority, int LineNumber);

internal interface IRedactionRuleSource
{
    IReadOnlyList<RedactionMatch> Find(string logicalPath, string content, RedactionLimits limits);
}

internal static class RedactionMetadata
{
    public const string SafePath = "***REDACTED***";
}
