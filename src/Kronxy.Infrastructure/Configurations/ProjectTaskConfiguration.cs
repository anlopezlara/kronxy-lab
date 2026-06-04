using Kronxy.Domain.ProjectTasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Kronxy.Infrastructure.Configurations;
internal sealed class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    public void Configure(EntityTypeBuilder<ProjectTask> builder)
    {
        builder.ToTable("project_tasks");
        builder.HasKey(projectTask => projectTask.Id);
        builder.Property(projectTask => projectTask.Id)
            .HasColumnName("id");
        builder.Property(projectTask => projectTask.ProjectId)
            .HasColumnName("project_id");
        builder.Property(projectTask => projectTask.AssignedUserId)
            .HasColumnName("assigned_user_id");
        builder.Property(projectTask => projectTask.Title)
            .HasMaxLength(200)
            .HasColumnName("title");
        builder.Property(projectTask => projectTask.Description)
            .HasMaxLength(2000)
            .HasColumnName("description");
        builder.Property(projectTask => projectTask.Status)
            .HasConversion<int>()
            .HasColumnName("status");
        builder.Property(projectTask => projectTask.Priority)
            .HasConversion<int>()
            .HasColumnName("priority");
        builder.Property(projectTask => projectTask.DueDate)
            .HasColumnName("due_date");
        builder.Property(projectTask => projectTask.EstimatedHours)
            .HasPrecision(10, 2)
            .HasColumnName("estimated_hours");
        builder.Property(projectTask => projectTask.WorkedHours)
            .HasPrecision(10, 2)
            .HasColumnName("worked_hours");
        builder.Property(projectTask => projectTask.IsActive)
            .HasColumnName("is_active");
        builder.Property(projectTask => projectTask.CreatedOnUtc)
            .HasColumnName("created_on_utc");
        builder.Property(projectTask => projectTask.UpdatedOnUtc)
            .HasColumnName("updated_on_utc");
        builder.Property(projectTask => projectTask.DeletedOnUtc)
            .HasColumnName("deleted_on_utc");
        builder.HasIndex(projectTask => projectTask.ProjectId);
        builder.HasIndex(projectTask => projectTask.AssignedUserId);
        builder.HasOne<Kronxy.Domain.Projects.Project>()
            .WithMany()
            .HasForeignKey(projectTask => projectTask.ProjectId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_project_tasks_projects_project_id");
        builder.HasOne<Kronxy.Domain.Users.User>()
            .WithMany()
            .HasForeignKey(projectTask => projectTask.AssignedUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_project_tasks_users_assigned_user_id");
    }
}
