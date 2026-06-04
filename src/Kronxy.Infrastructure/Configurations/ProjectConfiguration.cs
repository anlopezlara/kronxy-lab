using Kronxy.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Kronxy.Infrastructure.Configurations;
internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");
        builder.HasKey(project => project.Id);
        builder.Property(project => project.Id)
            .HasColumnName("id");
        builder.Property(project => project.Code)
            .HasMaxLength(50)
            .HasColumnName("code");
        builder.Property(project => project.Name)
            .HasMaxLength(200)
            .HasColumnName("name");
        builder.Property(project => project.Description)
            .HasMaxLength(2000)
            .HasColumnName("description");
        builder.Property(project => project.OwnerId)
            .HasColumnName("owner_id");
        builder.Property(project => project.ProjectTypeId)
            .IsRequired()
            .HasColumnName("project_type_id");
        builder.Property(project => project.Status)
            .HasConversion<int>()
            .HasColumnName("status");
        builder.Property(project => project.Priority)
            .HasConversion<int>()
            .HasColumnName("priority");
        builder.Property(project => project.StartDate)
            .HasColumnName("start_date");
        builder.Property(project => project.EndDate)
            .HasColumnName("end_date");
        builder.Property(project => project.IsActive)
            .HasColumnName("is_active");
        builder.Property(project => project.CreatedOnUtc)
            .HasColumnName("created_on_utc");
        builder.Property(project => project.UpdatedOnUtc)
            .HasColumnName("updated_on_utc");
        builder.Property(project => project.DeletedOnUtc)
            .HasColumnName("deleted_on_utc");
        builder.HasIndex(project => project.Code)
            .IsUnique();
        builder.HasIndex(project => project.OwnerId);
        builder.HasIndex(project => project.ProjectTypeId);
        builder.HasOne<Kronxy.Domain.Users.User>()
            .WithMany()
            .HasForeignKey(project => project.OwnerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_projects_users_owner_id");
        builder.HasOne<Kronxy.Domain.Catalogs.CatalogItem>()
            .WithMany()
            .HasForeignKey(project => project.ProjectTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_projects_catalog_items_project_type_id");
    }
}
