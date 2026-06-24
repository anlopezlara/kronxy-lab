using Kronxy.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Kronxy.Infrastructure.Configurations;
internal sealed class ProjectTypeConfiguration : IEntityTypeConfiguration<ProjectType>
{
    public void Configure(EntityTypeBuilder<ProjectType> builder)
    {
        builder.ToTable("project_types");
        builder.HasKey(projectType => projectType.Id);
        builder.Property(projectType => projectType.Id).HasColumnName("id");
        builder.Property(projectType => projectType.Code).HasMaxLength(50).HasColumnName("code");
        builder.Property(projectType => projectType.Name).HasMaxLength(200).HasColumnName("name");
        builder.Property(projectType => projectType.Description).HasMaxLength(1000).HasColumnName("description");
        builder.Property(projectType => projectType.DisplayOrder).HasColumnName("display_order");
        builder.Property(projectType => projectType.IsActive).HasColumnName("is_active");
        builder.Property(projectType => projectType.CreatedOnUtc).HasColumnName("created_on_utc");
        builder.Property(projectType => projectType.UpdatedOnUtc).HasColumnName("updated_on_utc");
        builder.Property(projectType => projectType.DeletedOnUtc).HasColumnName("deleted_on_utc");
        builder.HasIndex(projectType => projectType.Code).IsUnique();
        builder.HasIndex(projectType => projectType.DisplayOrder);
    }
}
