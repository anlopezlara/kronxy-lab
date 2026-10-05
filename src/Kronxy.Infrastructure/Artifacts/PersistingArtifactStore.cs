using System.Security.Cryptography;
using Kronxy.Application.Artifacts;

namespace Kronxy.Infrastructure.Artifacts;

public sealed class PersistingArtifactStore :
    IArtifactStore
{
    private readonly FileSystemArtifactStore fileStore;
    private readonly IArtifactMetadataRepository metadataRepository;
    private readonly IArtifactReader artifactReader;

    public PersistingArtifactStore(
        FileSystemArtifactStore fileStore,
        IArtifactMetadataRepository metadataRepository,
        IArtifactReader artifactReader)
    {
        this.fileStore =
            fileStore ??
            throw new ArgumentNullException(
                nameof(fileStore));

        this.metadataRepository =
            metadataRepository ??
            throw new ArgumentNullException(
                nameof(metadataRepository));

        this.artifactReader =
            artifactReader ??
            throw new ArgumentNullException(
                nameof(artifactReader));
    }

    public async Task<ArtifactWriteResult> WriteAsync(
        ArtifactWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArtifactWriteResult write =
            await fileStore
                .WriteAsync(
                    request,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!write.IsSuccess ||
            write.Artifact is null)
        {
            if (write.FailureKind ==
                ArtifactStoreFailureKind.DestinationExists)
            {
                return await ReconcileExistingAsync(
                        request,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return write;
        }

        try
        {
            await metadataRepository
                .AddAsync(
                    write.Artifact,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return ArtifactWriteResult.Failure(
                ArtifactStoreFailureKind.Cancelled,
                "ARTIFACT_METADATA_PERSISTENCE_CANCELLED");
        }
        catch
        {
            return ArtifactWriteResult.Failure(
                ArtifactStoreFailureKind.IoFailure,
                "ARTIFACT_METADATA_PERSISTENCE_FAILED");
        }

        return write;
    }
    private async Task<ArtifactWriteResult>
        ReconcileExistingAsync(
            ArtifactWriteRequest request,
            CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<ArtifactRecord> artifacts =
                await metadataRepository
                    .GetByJobAndRunAsync(
                        request.JobId,
                        request.RunId,
                        cancellationToken)
                    .ConfigureAwait(false);

            ArtifactRecord[] candidates =
                artifacts
                    .Where(
                        item =>
                            item.ArtifactType ==
                                request.ArtifactType)
                    .Where(
                        item =>
                            (request.ArtifactType !=
                                ArtifactType.PlanningRejectedResponse &&
                             !request.ArtifactType
                                .IsVersionedDeveloperCorrection() &&
                             !request.ArtifactType.IsVersionedHumanCorrection()) ||
                            string.Equals(
                                item.CorrelationId,
                                request.CorrelationId,
                                StringComparison.Ordinal))
                    .ToArray();

            if (candidates.Length != 1)
            {
                return ArtifactWriteResult.Failure(
                    ArtifactStoreFailureKind
                        .DestinationExists,
                    "ARTIFACT_EXISTING_METADATA_NOT_FOUND");
            }

            ArtifactRecord existing =
                candidates[0];

            string expectedHash =
                Convert.ToHexString(
                    SHA256.HashData(
                        request.Content.Span))
                .ToLowerInvariant();

            if (existing.SizeBytes !=
                    request.Content.Length ||
                !string.Equals(
                    existing.Sha256,
                    expectedHash,
                    StringComparison.Ordinal))
            {
                return ArtifactWriteResult.Failure(
                    ArtifactStoreFailureKind
                        .IntegrityFailure,
                    "ARTIFACT_EXISTING_CONTENT_CONFLICT");
            }

            ArtifactReadResult read =
                await artifactReader
                    .ReadAsync(
                        new ArtifactReadRequest
                        {
                            JobId =
                                request.JobId,

                            RunId =
                                request.RunId,

                            ArtifactType =
                                request.ArtifactType,

                            MaxBytes =
                                Math.Max(
                                    1,
                                    request.Content.Length),

                            CorrelationId =
                                request.CorrelationId
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!read.IsSuccess)
            {
                return ArtifactWriteResult.Failure(
                    ArtifactStoreFailureKind
                        .IntegrityFailure,
                    "ARTIFACT_EXISTING_INTEGRITY_FAILED");
            }

            if (read.Content.Length !=
                    request.Content.Length ||
                !CryptographicOperations
                    .FixedTimeEquals(
                        read.Content.Span,
                        request.Content.Span))
            {
                return ArtifactWriteResult.Failure(
                    ArtifactStoreFailureKind
                        .IntegrityFailure,
                    "ARTIFACT_EXISTING_BYTES_CONFLICT");
            }

            return ArtifactWriteResult.Success(
                existing);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            return ArtifactWriteResult.Failure(
                ArtifactStoreFailureKind.Cancelled,
                "ARTIFACT_RECONCILIATION_CANCELLED");
        }
        catch
        {
            return ArtifactWriteResult.Failure(
                ArtifactStoreFailureKind.IoFailure,
                "ARTIFACT_RECONCILIATION_FAILED");
        }
    }

}
