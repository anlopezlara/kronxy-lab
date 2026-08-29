using System.Text.Json.Serialization;
using Kronxy.Context.Models;
using Kronxy.Context.Redaction;

namespace Kronxy.Context.Packaging;

public enum PackageEntryKind
{
    Source,
    Patch,
    Diagnostic,
    Metadata
}

public enum PackageBuildStatus
{
    Success, InvalidRequest, InvalidPath, DuplicateEntry, RedactionFailed,
    SensitiveContentRemaining, LimitExceeded, DestinationExists, Cancelled,
    IntegrityFailure, IoFailure, InternalFailure
}

public sealed record PackageBuildLimits
{
    public int MaxEntries { get; init; } = 100;
    public int MaxEntryBytes { get; init; } = 262_144;
    public long MaxTotalContentBytes { get; init; } = 5_242_880;
    public int MaxManifestBytes { get; init; } = 262_144;
    public long MaxPackageBytes { get; init; } = 6_291_456;
    public int MaxPathBytes { get; init; } = 1_024;
    public RedactionLimits Redaction { get; init; } = new();

    internal bool IsValid() => MaxEntries > 0 && MaxEntryBytes > 0 && MaxTotalContentBytes > 0 &&
        MaxManifestBytes > 0 && MaxPackageBytes > 0 && MaxPathBytes > 0 && Redaction is not null &&
        Redaction.IsValid() && MaxEntryBytes <= MaxTotalContentBytes && MaxManifestBytes <= MaxPackageBytes;
}

public sealed record PackageEntryRequest
{
    [JsonIgnore]
    public string LogicalPath { get; init; } = string.Empty;
    public required PackageEntryKind Kind { get; init; }
    [JsonIgnore]
    public string Content { get; init; } = string.Empty;
    public override string ToString() => $"PackageEntryRequest {{ LogicalPath = ***REDACTED***, Kind = {Kind}, Content = ***REDACTED*** }}";
}

public sealed record PackageBuildRequest
{
    [JsonIgnore]
    public string DestinationPath { get; init; } = string.Empty;
    public required PackageType PackageType { get; init; }
    public required PackageStability Stability { get; init; }
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public required IReadOnlyList<PackageEntryRequest> Entries { get; init; }
    public PackageBuildLimits Limits { get; init; } = new();
    public override string ToString() => $"PackageBuildRequest {{ DestinationPath = ***REDACTED***, Entries = {Entries?.Count ?? 0} }}";
}

public sealed record PackageBuildResult
{
    public required PackageBuildStatus Status { get; init; }
    public string? PackageSha256 { get; init; }
    public long? PackageSizeBytes { get; init; }
    public int EntryCount { get; init; }
    public bool IsSuccess => Status == PackageBuildStatus.Success;
    public override string ToString() => $"PackageBuildResult {{ Status = {Status}, EntryCount = {EntryCount} }}";
}

public interface IPackageBuilder
{
    Task<PackageBuildResult> BuildAsync(PackageBuildRequest request, CancellationToken cancellationToken = default);
}

internal sealed record PreparedPackageEntry(
    [property: JsonIgnore] string LogicalPath, PackageEntryKind Kind,
    [property: JsonIgnore] byte[] Content, string Sha256,
    IReadOnlyList<PreparedRedactionSummary> Redactions)
{
    public override string ToString() =>
        $"PreparedPackageEntry {{ LogicalPath = ***REDACTED***, Kind = {Kind}, Content = ***REDACTED*** }}";
}

internal sealed record PreparedRedactionSummary(string Category, int Count);

internal sealed record CanonicalPackageManifest
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentGeneratorVersion = "0.1.0";

    [JsonPropertyOrder(0), JsonPropertyName("schemaVersion")]
    public required string SchemaVersion { get; init; }
    [JsonPropertyOrder(1), JsonPropertyName("generatorVersion")]
    public required string GeneratorVersion { get; init; }
    [JsonPropertyOrder(2), JsonPropertyName("packageId")]
    public required string PackageId { get; init; }
    [JsonPropertyOrder(3), JsonPropertyName("packageType")]
    public required string PackageType { get; init; }
    [JsonPropertyOrder(4), JsonPropertyName("stability")]
    public required string Stability { get; init; }
    [JsonPropertyOrder(5), JsonPropertyName("generatedAtUtc")]
    public required string GeneratedAtUtc { get; init; }
    [JsonPropertyOrder(6), JsonPropertyName("fileCount")]
    public required int FileCount { get; init; }
    [JsonPropertyOrder(7), JsonPropertyName("totalContentBytes")]
    public required long TotalContentBytes { get; init; }
    [JsonPropertyOrder(8), JsonPropertyName("entries")]
    public required IReadOnlyList<CanonicalManifestEntry> Entries { get; init; }
}

internal sealed record CanonicalManifestEntry
{
    [JsonPropertyOrder(0), JsonPropertyName("path")]
    public required string Path { get; init; }
    [JsonPropertyOrder(1), JsonPropertyName("kind")]
    public required string Kind { get; init; }
    [JsonPropertyOrder(2), JsonPropertyName("sizeBytes")]
    public required int SizeBytes { get; init; }
    [JsonPropertyOrder(3), JsonPropertyName("sha256")]
    public required string Sha256 { get; init; }
    [JsonPropertyOrder(4), JsonPropertyName("redactions")]
    public required IReadOnlyList<CanonicalRedactionSummary> Redactions { get; init; }
}

internal sealed record CanonicalRedactionSummary
{
    [JsonPropertyOrder(0), JsonPropertyName("category")]
    public required string Category { get; init; }
    [JsonPropertyOrder(1), JsonPropertyName("count")]
    public required int Count { get; init; }
}
