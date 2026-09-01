namespace Kronxy.Application.Artifacts;

public enum ArtifactType
{
    ContextPackage = 10,
    AiResponse = 20,

    RestoreReport = 30,
    RestoreStandardOutput = 31,
    RestoreStandardError = 32,

    BuildReport = 40,
    BuildStandardOutput = 41,
    BuildStandardError = 42,

    TestReport = 50,
    TestResults = 51,
    TestStandardOutput = 52,
    TestStandardError = 53,

    GeneralReport = 60
}

public enum ArtifactStoreFailureKind
{
    None = 0,
    InvalidRequest = 10,
    UnsafeRoot = 20,
    UnsafePath = 30,
    ArtifactTooLarge = 40,
    DestinationExists = 50,
    IntegrityFailure = 60,
    IoFailure = 70,
    Cancelled = 80
}

public sealed record ArtifactWriteRequest
{
    public required Guid JobId { get; init; }

    public required Guid RunId { get; init; }

    public required ArtifactType ArtifactType { get; init; }

    public required ReadOnlyMemory<byte> Content { get; init; }

    public string CorrelationId { get; init; } = string.Empty;
}

public sealed record ArtifactRecord
{
    public required Guid ArtifactId { get; init; }

    public required Guid JobId { get; init; }

    public required Guid RunId { get; init; }

    public required ArtifactType ArtifactType { get; init; }

    public required string RelativePath { get; init; }

    public required string Sha256 { get; init; }

    public required long SizeBytes { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required string CorrelationId { get; init; }
}

public sealed record ArtifactWriteResult(
    ArtifactRecord? Artifact,
    ArtifactStoreFailureKind FailureKind,
    string ErrorCode)
{
    public bool IsSuccess =>
        FailureKind == ArtifactStoreFailureKind.None &&
        Artifact is not null;

    public static ArtifactWriteResult Success(
        ArtifactRecord artifact) =>
        new(
            artifact,
            ArtifactStoreFailureKind.None,
            string.Empty);

    public static ArtifactWriteResult Failure(
        ArtifactStoreFailureKind kind,
        string errorCode) =>
        new(
            null,
            kind,
            errorCode);
}

public interface IArtifactStore
{
    Task<ArtifactWriteResult> WriteAsync(
        ArtifactWriteRequest request,
        CancellationToken cancellationToken = default);
}

public interface IArtifactMetadataRepository
{
    Task AddAsync(
        ArtifactRecord artifact,
        CancellationToken cancellationToken = default);

    Task<ArtifactRecord?> GetByIdAsync(
        Guid artifactId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ArtifactRecord>> GetByJobAndRunAsync(
        Guid jobId,
        Guid runId,
        CancellationToken cancellationToken = default);
}
