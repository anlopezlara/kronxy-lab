using System.Text.Json.Serialization;

namespace Kronxy.Context.Models;

public sealed record PackageManifest
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = "1.0";
    [JsonPropertyName("packageId")]
    public string PackageId { get; init; } = string.Empty;
    [JsonPropertyName("packageType")]
    public PackageType PackageType { get; init; }
    [JsonPropertyName("stability")]
    public PackageStability Stability { get; init; }
    [JsonPropertyName("generatorVersion")]
    public string GeneratorVersion { get; init; } = "0.1.0";
    [JsonPropertyName("repositoryName")]
    public string RepositoryName { get; init; } = string.Empty;
    [JsonPropertyName("repositoryRoot")]
    public string RepositoryRoot { get; init; } = ".";
    [JsonPropertyName("branch")]
    public string Branch { get; init; } = string.Empty;
    [JsonPropertyName("baseCommit")]
    public string? BaseCommit { get; init; }
    [JsonPropertyName("targetCommit")]
    public string? TargetCommit { get; init; }
    [JsonPropertyName("isDirty")]
    public bool IsDirty { get; init; }
    [JsonPropertyName("hasStagedChanges")]
    public bool HasStagedChanges { get; init; }
    [JsonPropertyName("hasUntrackedFiles")]
    public bool HasUntrackedFiles { get; init; }
    [JsonPropertyName("workItem")]
    public string? WorkItem { get; init; }
    [JsonPropertyName("generatedAtUtc")]
    public DateTimeOffset GeneratedAtUtc { get; init; }
    [JsonPropertyName("includedFiles")]
    public List<ManifestFileEntry> IncludedFiles { get; init; } = [];
    [JsonPropertyName("excludedFiles")]
    public List<ExcludedFileEntry> ExcludedFiles { get; init; } = [];
    [JsonPropertyName("redactions")]
    public List<RedactionRecord> Redactions { get; init; } = [];
    [JsonPropertyName("commands")]
    public List<CommandResult> Commands { get; init; } = [];
    [JsonPropertyName("validationResults")]
    public List<ValidationResult> ValidationResults { get; init; } = [];
    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; init; } = [];
}
