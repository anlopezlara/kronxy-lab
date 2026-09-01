using Kronxy.Application.Repositories;

namespace Kronxy.Application.Context;

public enum ContextGenerationFailureKind
{
    None = 0,
    InvalidRequest = 10,
    RepositoryMismatch = 20,
    InventoryFailure = 30,
    FileReadFailure = 40,
    SensitiveContentDetected = 50,
    PackageLimitExceeded = 60,
    InvalidPackage = 70,
    ArtifactWriteFailure = 80,
    TimedOut = 90,
    Cancelled = 100,
    InternalFailure = 110
}

public sealed record ContextGenerationRequest
{
    public required Guid JobId { get; init; }

    public required Guid RunId { get; init; }

    public required string JobExternalId { get; init; }

    public required RepositoryWorktreeHandle Repository { get; init; }

    public required string BaseRepositoryHead { get; init; }

    public string CorrelationId { get; init; } = string.Empty;
}

public sealed record ContextPackageArtifact
{
    public required string PackageId { get; init; }

    public required string RelativePath { get; init; }

    public required string Sha256 { get; init; }

    public required long SizeBytes { get; init; }

    public required int EntryCount { get; init; }

    public required string GeneratorVersion { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed record ContextGenerationResult(
    ContextPackageArtifact? Artifact,
    ContextGenerationFailureKind FailureKind,
    string ErrorCode)
{
    public bool IsSuccess =>
        FailureKind == ContextGenerationFailureKind.None &&
        Artifact is not null;

    public static ContextGenerationResult Success(
        ContextPackageArtifact artifact) =>
        new(
            artifact,
            ContextGenerationFailureKind.None,
            string.Empty);

    public static ContextGenerationResult Failure(
        ContextGenerationFailureKind kind,
        string errorCode) =>
        new(
            null,
            kind,
            errorCode);
}

public interface IContextGenerationService
{
    Task<ContextGenerationResult> GenerateAsync(
        ContextGenerationRequest request,
        CancellationToken cancellationToken = default);
}
