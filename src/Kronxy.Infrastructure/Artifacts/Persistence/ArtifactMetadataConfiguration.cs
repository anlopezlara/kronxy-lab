using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kronxy.Infrastructure.Artifacts.Persistence;

internal sealed class ArtifactMetadataConfiguration :
    IEntityTypeConfiguration<ArtifactMetadataEntity>
{
    public void Configure(
        EntityTypeBuilder<ArtifactMetadataEntity> builder)
    {
        builder.ToTable("job_artifacts");

        builder.HasKey(
            artifact => artifact.ArtifactId);

        builder.Property(
                artifact => artifact.ArtifactId)
            .HasColumnName("artifact_id");

        builder.Property(
                artifact => artifact.JobId)
            .HasColumnName("job_id")
            .IsRequired();

        builder.Property(
                artifact => artifact.RunId)
            .HasColumnName("run_id")
            .IsRequired();

        builder.Property(
                artifact => artifact.ArtifactType)
            .HasColumnName("artifact_type")
            .IsRequired();

        builder.Property(
                artifact => artifact.RelativePath)
            .HasColumnName("relative_path")
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(
                artifact => artifact.Sha256)
            .HasColumnName("sha256")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(
                artifact => artifact.SizeBytes)
            .HasColumnName("size_bytes")
            .IsRequired();

        builder.Property(
                artifact => artifact.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(
                artifact => artifact.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(
                artifact => artifact.JobId)
            .HasDatabaseName(
                "ix_job_artifacts_job_id");

        builder.HasIndex(
                artifact => new
                {
                    artifact.JobId,
                    artifact.RunId
                })
            .HasDatabaseName(
                "ix_job_artifacts_job_id_run_id");

        builder.HasIndex(
                artifact => artifact.RelativePath)
            .IsUnique()
            .HasDatabaseName(
                "ux_job_artifacts_relative_path");

        builder.HasIndex(
                artifact => new
                {
                    artifact.JobId,
                    artifact.RunId,
                    artifact.ArtifactType
                })
            .HasDatabaseName(
                "ix_job_artifacts_job_run_type");
    }
}
