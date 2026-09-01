namespace Kronxy.Infrastructure.Artifacts;

public sealed record ArtifactStoreOptions
{
    public const string SectionName = "ArtifactStore";

    public required string RootPath { get; init; }

    public long MaxArtifactBytes { get; init; } = 16_777_216;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RootPath) ||
            RootPath.IndexOfAny(['\0', '\r', '\n']) >= 0 ||
            !Path.IsPathFullyQualified(RootPath))
        {
            throw new InvalidOperationException(
                "ArtifactStore RootPath is invalid.");
        }

        if (MaxArtifactBytes <= 0)
        {
            throw new InvalidOperationException(
                "ArtifactStore MaxArtifactBytes must be greater than zero.");
        }

        try
        {
            _ = Path.GetFullPath(RootPath);
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                  NotSupportedException or
                  PathTooLongException)
        {
            throw new InvalidOperationException(
                "ArtifactStore RootPath is invalid.",
                exception);
        }
    }
}
