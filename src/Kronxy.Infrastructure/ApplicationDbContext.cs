using Kronxy.Application.Exceptions;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Jobs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kronxy.Infrastructure;

public sealed class ApplicationDbContext : DbContext, IUnitOfWork
{
    private readonly IPublisher _publisher;

    public ApplicationDbContext(DbContextOptions options, IPublisher publisher)
        : base(options)
    {
        _publisher = publisher;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasSequence<long>("job_external_id_seq")
            .StartsAt(1)
            .IncrementsBy(1);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IncrementJobVersions();

            var result = await base.SaveChangesAsync(cancellationToken);

            await PublishDomainEventsAsync();

            return result;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Concurrency exception occurred.", ex);
        }
    }

    private void IncrementJobVersions()
    {
        foreach (var entry in ChangeTracker.Entries<Job>())
        {
            if (entry.State == EntityState.Added && entry.Entity.Version <= 0)
            {
                entry.Property(job => job.Version).CurrentValue = 1;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(job => job.Version).CurrentValue =
                    entry.Property(job => job.Version).OriginalValue + 1;
            }
        }
    }

    private async Task PublishDomainEventsAsync()
    {
        var domainEvents = ChangeTracker
            .Entries<Entity>()
            .Select(entry => entry.Entity)
            .SelectMany(entity =>
            {
                var domainEvents = entity.GetDomainEvents();

                entity.ClearDomainEvents();

                return domainEvents;
            })
            .ToList();

        foreach (var domainEvent in domainEvents)
        {
            await _publisher.Publish(domainEvent);
        }
    }
}
