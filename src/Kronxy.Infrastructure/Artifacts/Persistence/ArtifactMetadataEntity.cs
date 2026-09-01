using Kronxy.Application.Artifacts;

namespace Kronxy.Infrastructure.Artifacts.Persistence;

internal sealed class ArtifactMetadataEntity
{
    public Guid ArtifactId { get; set; }

    public Guid JobId { get; set; }

    public Guid RunId { get; set; }

    public ArtifactType ArtifactType { get; set; }

    public string RelativePath { get; set; } =
        string.Empty;

    public string Sha256 { get; set; } =
        string.Empty;

    public long SizeBytes { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string CorrelationId { get; set; } =
        string.Empty;
}
