using Kronxy.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Kronxy.Infrastructure.Configurations;
internal sealed class ProjectPriorityConfiguration : IEntityTypeConfiguration<ProjectPriority>
{
    public void Configure(EntityTypeBuilder<ProjectPriority> builder)
    {
        builder.ToTable("project_priorities");
        builder.HasKey(projectPriority => projectPriority.Id);
        builder.Property(projectPriority => projectPriority.Id).HasColumnName("id");
        builder.Property(projectPriority => projectPriority.Code).HasMaxLength(50).HasColumnName("code");
        builder.Property(projectPriority => projectPriority.Name).HasMaxLength(200).HasColumnName("name");
        builder.Property(projectPriority => projectPriority.Description).HasMaxLength(1000).HasColumnName("description");
        builder.Property(projectPriority => projectPriority.DisplayOrder).HasColumnName("display_order");
        builder.Property(projectPriority => projectPriority.IsActive).HasColumnName("is_active");
        builder.Property(projectPriority => projectPriority.CreatedOnUtc).HasColumnName("created_on_utc");
        builder.Property(projectPriority => projectPriority.UpdatedOnUtc).HasColumnName("updated_on_utc");
        builder.Property(projectPriority => projectPriority.DeletedOnUtc).HasColumnName("deleted_on_utc");
        builder.HasIndex(projectPriority => projectPriority.Code).IsUnique();
        builder.HasIndex(projectPriority => projectPriority.DisplayOrder);
    }
}
