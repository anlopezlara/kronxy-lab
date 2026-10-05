using Kronxy.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kronxy.Infrastructure.Configurations;

internal sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("jobs");

        builder.HasKey(job => job.Id);

        builder.Ignore(job => job.Transitions);

        builder.Property(job => job.Id)
            .HasColumnName("id");

        builder.Property(job => job.ExternalId)
            .HasColumnName("external_id")
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(job => job.ExternalId)
            .IsUnique();

        builder.Property(job => job.Request)
            .HasColumnName("request")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(job => job.State)
            .HasColumnName("state")
            .IsRequired();

        builder.Property(job => job.ResumeState)
            .HasColumnName("resume_state");

        builder.Property(job => job.AttemptCount)
            .HasColumnName("attempt_count")
            .IsRequired();

        builder.Property(job => job.CreatedOnUtc)
            .HasColumnName("created_on_utc")
            .IsRequired();

        builder.Property(job => job.UpdatedOnUtc)
            .HasColumnName("updated_on_utc")
            .IsRequired();

        builder.Property(job => job.ActiveExecutionStartedOnUtc)
            .HasColumnName("active_execution_started_on_utc");

        builder.Property(job => job.LastActiveProgressOnUtc)
            .HasColumnName("last_active_progress_on_utc");

        builder.Property(job => job.CompletedOnUtc)
            .HasColumnName("completed_on_utc");

        builder.Property(job => job.BaseRepositoryHead)
            .HasColumnName("base_repository_head")
            .HasMaxLength(64);

        builder.Property(job => job.LastErrorCode)
            .HasColumnName("last_error_code")
            .HasMaxLength(200);

        builder.Property(job => job.LastErrorMessage)
            .HasColumnName("last_error_message")
            .HasColumnType("text");

        builder.Property(job => job.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .IsRequired();

        builder.OwnsOne(job => job.Limits, limits =>
        {
            limits.Property(value => value.MaxJobDuration)
                .HasColumnName("max_job_duration")
                .IsRequired();

            limits.Property(value => value.MaxAttempts)
                .HasColumnName("max_attempts")
                .IsRequired();

            limits.Property(value => value.MaxAgentIterations)
                .HasColumnName("max_agent_iterations")
                .IsRequired();

            limits.Property(value => value.MaxAiCalls)
                .HasColumnName("max_ai_calls")
                .IsRequired();
        });

        builder.OwnsMany<JobTransition>("_transitions", transition =>
        {
            transition.ToTable("job_transitions");

            transition.WithOwner()
                .HasForeignKey("job_id");

            transition.Property<long>("id")
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            transition.HasKey("id");

            transition.Property<Guid>("job_id")
                .HasColumnName("job_id")
                .IsRequired();

            transition.Property(item => item.FromState)
                .HasColumnName("from_state")
                .IsRequired();

            transition.Property(item => item.ToState)
                .HasColumnName("to_state")
                .IsRequired();

            transition.Property(item => item.OccurredOnUtc)
                .HasColumnName("occurred_on_utc")
                .IsRequired();

            transition.Property(item => item.Reason)
                .HasColumnName("reason")
                .HasColumnType("text")
                .IsRequired();

            transition.Property(item => item.Actor)
                .HasColumnName("actor")
                .HasMaxLength(200)
                .IsRequired();

            transition.Property(item => item.CorrelationId)
                .HasColumnName("correlation_id")
                .HasMaxLength(200)
                .IsRequired();

            transition.HasIndex("job_id");
        });

        builder.Navigation("_transitions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
