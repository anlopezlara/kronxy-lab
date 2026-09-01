namespace Kronxy.Application.Artifacts;

public enum ArtifactReadFailureKind
{
    None = 0,
    InvalidRequest = 10,
    NotFound = 20,
    UnsafeRoot = 30,
    UnsafePath = 40,
    SymlinkEscape = 50,
    IntegrityFailure = 60,
    TooLarge = 70,
    IoFailure = 80,
    Cancelled = 90
}

public sealed record ArtifactReadRequest
{
    public required Guid JobId { get; init; }

    public required Guid RunId { get; init; }

    public required ArtifactType ArtifactType { get; init; }

    public long MaxBytes { get; init; }

    public string CorrelationId { get; init; } =
        string.Empty;
}

public sealed record ArtifactReadResult(
    ArtifactRecord? Artifact,
    ReadOnlyMemory<byte> Content,
    ArtifactReadFailureKind FailureKind,
    string ErrorCode)
{
    public bool IsSuccess =>
        FailureKind == ArtifactReadFailureKind.None &&
        Artifact is not null;

    public static ArtifactReadResult Success(
        ArtifactRecord artifact,
        ReadOnlyMemory<byte> content) =>
        new(
            artifact,
            content,
            ArtifactReadFailureKind.None,
            string.Empty);

    public static ArtifactReadResult Failure(
        ArtifactReadFailureKind kind,
        string errorCode) =>
        new(
            null,
            ReadOnlyMemory<byte>.Empty,
            kind,
            errorCode);
}

public interface IArtifactReader
{
    Task<ArtifactReadResult> ReadAsync(
        ArtifactReadRequest request,
        CancellationToken cancellationToken = default);
}
