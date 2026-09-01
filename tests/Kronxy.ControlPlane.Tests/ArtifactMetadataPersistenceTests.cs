using Kronxy.Infrastructure;
using Kronxy.Infrastructure.Artifacts.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class ArtifactMetadataPersistenceTests
{
    [Fact]
    public void Entity_model_uses_expected_table_columns_indexes_and_lengths()
    {
        using ApplicationDbContext dbContext =
            CreateModelContext();

        IEntityType entity =
            Assert.IsAssignableFrom<IEntityType>(
                dbContext.Model.FindEntityType(
                    typeof(
                        ArtifactMetadataEntity)));

        Assert.Equal(
            "job_artifacts",
            entity.GetTableName());

        Assert.Equal(
            "artifact_id",
            entity.FindProperty(
                    nameof(
                        ArtifactMetadataEntity
                            .ArtifactId))!
                .GetColumnName());

        Assert.Equal(
            "job_id",
            entity.FindProperty(
                    nameof(
                        ArtifactMetadataEntity
                            .JobId))!
                .GetColumnName());

        Assert.Equal(
            "run_id",
            entity.FindProperty(
                    nameof(
                        ArtifactMetadataEntity
                            .RunId))!
                .GetColumnName());

        Assert.Equal(
            "artifact_type",
            entity.FindProperty(
                    nameof(
                        ArtifactMetadataEntity
                            .ArtifactType))!
                .GetColumnName());

        Assert.Equal(
            "relative_path",
            entity.FindProperty(
                    nameof(
                        ArtifactMetadataEntity
                            .RelativePath))!
                .GetColumnName());

        Assert.Equal(
            1024,
            entity.FindProperty(
                    nameof(
                        ArtifactMetadataEntity
                            .RelativePath))!
                .GetMaxLength());

        Assert.Equal(
            "sha256",
            entity.FindProperty(
                    nameof(
                        ArtifactMetadataEntity
                            .Sha256))!
                .GetColumnName());

        Assert.Equal(
            64,
            entity.FindProperty(
                    nameof(
                        ArtifactMetadataEntity
                            .Sha256))!
                .GetMaxLength());

        Assert.Equal(
            "size_bytes",
            entity.FindProperty(
                    nameof(
                        ArtifactMetadataEntity
                            .SizeBytes))!
                .GetColumnName());

        Assert.Equal(
            "created_at_utc",
            entity.FindProperty(
                    nameof(
                        ArtifactMetadataEntity
                            .CreatedAtUtc))!
                .GetColumnName());

        Assert.Equal(
            "correlation_id",
            entity.FindProperty(
                    nameof(
                        ArtifactMetadataEntity
                            .CorrelationId))!
                .GetColumnName());

        Assert.Equal(
            200,
            entity.FindProperty(
                    nameof(
                        ArtifactMetadataEntity
                            .CorrelationId))!
                .GetMaxLength());

        Assert.Contains(
            entity.GetIndexes(),
            index =>
                index.IsUnique &&
                index.Properties.Count == 1 &&
                index.Properties[0].Name ==
                    nameof(
                        ArtifactMetadataEntity
                            .RelativePath));

        Assert.Contains(
            entity.GetIndexes(),
            index =>
                index.Properties
                    .Select(
                        property =>
                            property.Name)
                    .SequenceEqual(
                        [
                            nameof(
                                ArtifactMetadataEntity
                                    .JobId),
                            nameof(
                                ArtifactMetadataEntity
                                    .RunId)
                        ]));

        Assert.Contains(
            entity.GetIndexes(),
            index =>
                index.Properties
                    .Select(
                        property =>
                            property.Name)
                    .SequenceEqual(
                        [
                            nameof(
                                ArtifactMetadataEntity
                                    .JobId),
                            nameof(
                                ArtifactMetadataEntity
                                    .RunId),
                            nameof(
                                ArtifactMetadataEntity
                                    .ArtifactType)
                        ]));
    }

    private static ApplicationDbContext
        CreateModelContext()
    {
        var options =
            new DbContextOptionsBuilder<
                ApplicationDbContext>()
                .UseNpgsql(
                    "Host=127.0.0.1;" +
                    "Port=5432;" +
                    "Database=kronxy_model_only;" +
                    "Username=kronxy_model_only;" +
                    "Password=kronxy_model_only")
                .UseSnakeCaseNamingConvention()
                .Options;

        return new ApplicationDbContext(
            options,
            new NullPublisher());
    }

    private sealed class NullPublisher :
        MediatR.IPublisher
    {
        public Task Publish(
            object notification,
            CancellationToken cancellationToken =
                default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken =
                default)
            where TNotification :
                MediatR.INotification =>
            Task.CompletedTask;
    }
}
