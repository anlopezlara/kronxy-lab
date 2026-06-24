using Kronxy.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Kronxy.Infrastructure.Configurations;
internal sealed class ProjectStatusConfiguration : IEntityTypeConfiguration<ProjectStatus>
{
    public void Configure(EntityTypeBuilder<ProjectStatus> builder)
    {
        builder.ToTable("project_statuses");
        builder.HasKey(projectStatus => projectStatus.Id);
        builder.Property(projectStatus => projectStatus.Id)
            .HasColumnName("id");
        builder.Property(projectStatus => projectStatus.Code)
            .HasMaxLength(50)
            .HasColumnName("code");
        builder.Property(projectStatus => projectStatus.Name)
            .HasMaxLength(200)
            .HasColumnName("name");
        builder.Property(projectStatus => projectStatus.Description)
            .HasMaxLength(1000)
            .HasColumnName("description");
        builder.Property(projectStatus => projectStatus.DisplayOrder)
            .HasColumnName("display_order");
        builder.Property(projectStatus => projectStatus.IsActive)
            .HasColumnName("is_active");
        builder.Property(projectStatus => projectStatus.CreatedOnUtc)
            .HasColumnName("created_on_utc");
        builder.Property(projectStatus => projectStatus.UpdatedOnUtc)
            .HasColumnName("updated_on_utc");
        builder.Property(projectStatus => projectStatus.DeletedOnUtc)
            .HasColumnName("deleted_on_utc");
        builder.HasIndex(projectStatus => projectStatus.Code)
            .IsUnique();
        builder.HasIndex(projectStatus => projectStatus.DisplayOrder);
    }
}
