using Kronxy.Application.Artifacts;
using Microsoft.EntityFrameworkCore;

namespace Kronxy.Infrastructure.Artifacts.Persistence;

internal sealed class ArtifactMetadataRepository :
    IArtifactMetadataRepository
{
    private readonly ApplicationDbContext dbContext;

    public ArtifactMetadataRepository(
        ApplicationDbContext dbContext)
    {
        this.dbContext =
            dbContext ??
            throw new ArgumentNullException(
                nameof(dbContext));
    }

    public async Task AddAsync(
        ArtifactRecord artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            artifact);

        Validate(
            artifact);

        ArtifactMetadataEntity? existing =
            await dbContext
                .Set<ArtifactMetadataEntity>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item =>
                        item.ArtifactId ==
                            artifact.ArtifactId ||
                        item.RelativePath ==
                            artifact.RelativePath,
                    cancellationToken)
                .ConfigureAwait(false);

        if (existing is not null)
        {
            if (IsEquivalent(
                    existing,
                    artifact))
            {
                return;
            }

            throw new InvalidOperationException(
                "Artifact metadata conflict.");
        }

        var entity =
            new ArtifactMetadataEntity
            {
                ArtifactId =
                    artifact.ArtifactId,

                JobId =
                    artifact.JobId,

                RunId =
                    artifact.RunId,

                ArtifactType =
                    artifact.ArtifactType,

                RelativePath =
                    artifact.RelativePath,

                Sha256 =
                    artifact.Sha256,

                SizeBytes =
                    artifact.SizeBytes,

                CreatedAtUtc =
                    artifact.CreatedAtUtc,

                CorrelationId =
                    artifact.CorrelationId
            };

        await dbContext
            .Set<ArtifactMetadataEntity>()
            .AddAsync(
                entity,
                cancellationToken)
            .ConfigureAwait(false);

        await dbContext
            .SaveChangesAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ArtifactRecord?> GetByIdAsync(
        Guid artifactId,
        CancellationToken cancellationToken = default)
    {
        if (artifactId == Guid.Empty)
        {
            return null;
        }

        ArtifactMetadataEntity? entity =
            await dbContext
                .Set<ArtifactMetadataEntity>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item =>
                        item.ArtifactId ==
                            artifactId,
                    cancellationToken)
                .ConfigureAwait(false);

        return entity is null
            ? null
            : Map(entity);
    }

    public async Task<IReadOnlyList<ArtifactRecord>>
        GetByJobAndRunAsync(
            Guid jobId,
            Guid runId,
            CancellationToken cancellationToken = default)
    {
        if (jobId == Guid.Empty ||
            runId == Guid.Empty)
        {
            return [];
        }

        List<ArtifactMetadataEntity> entities =
            await dbContext
                .Set<ArtifactMetadataEntity>()
                .AsNoTracking()
                .Where(
                    item =>
                        item.JobId == jobId &&
                        item.RunId == runId)
                .OrderBy(
                    item =>
                        item.CreatedAtUtc)
                .ThenBy(
                    item =>
                        item.ArtifactId)
                .ToListAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return entities
            .Select(Map)
            .ToArray();
    }

    private static bool IsEquivalent(
        ArtifactMetadataEntity existing,
        ArtifactRecord artifact)
    {
        return
            existing.ArtifactId ==
                artifact.ArtifactId &&
            existing.JobId ==
                artifact.JobId &&
            existing.RunId ==
                artifact.RunId &&
            existing.ArtifactType ==
                artifact.ArtifactType &&
            string.Equals(
                existing.RelativePath,
                artifact.RelativePath,
                StringComparison.Ordinal) &&
            string.Equals(
                existing.Sha256,
                artifact.Sha256,
                StringComparison.Ordinal) &&
            existing.SizeBytes ==
                artifact.SizeBytes &&
            existing.CreatedAtUtc ==
                artifact.CreatedAtUtc &&
            string.Equals(
                existing.CorrelationId,
                artifact.CorrelationId,
                StringComparison.Ordinal);
    }

    private static ArtifactRecord Map(
        ArtifactMetadataEntity entity) =>
        new()
        {
            ArtifactId =
                entity.ArtifactId,

            JobId =
                entity.JobId,

            RunId =
                entity.RunId,

            ArtifactType =
                entity.ArtifactType,

            RelativePath =
                entity.RelativePath,

            Sha256 =
                entity.Sha256,

            SizeBytes =
                entity.SizeBytes,

            CreatedAtUtc =
                entity.CreatedAtUtc,

            CorrelationId =
                entity.CorrelationId
        };

    private static void Validate(
        ArtifactRecord artifact)
    {
        if (artifact.ArtifactId ==
                Guid.Empty ||
            artifact.JobId ==
                Guid.Empty ||
            artifact.RunId ==
                Guid.Empty ||
            string.IsNullOrWhiteSpace(
                artifact.RelativePath) ||
            string.IsNullOrWhiteSpace(
                artifact.Sha256) ||
            artifact.Sha256.Length != 64 ||
            artifact.SizeBytes < 0)
        {
            throw new ArgumentException(
                "Artifact metadata is invalid.",
                nameof(artifact));
        }
    }
}
