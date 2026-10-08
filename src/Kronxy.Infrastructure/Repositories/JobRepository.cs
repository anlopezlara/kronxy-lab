using Kronxy.Domain.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Kronxy.Infrastructure.Repositories;

internal sealed class JobRepository : Repository<Job>, IJobRepository
{
    public JobRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<Job?> GetByExternalIdAsync(
        string externalId,
        CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<Job>()
            .FirstOrDefaultAsync(
                job => job.ExternalId == externalId,
                cancellationToken);
    }

    public async Task<(IReadOnlyList<Job> Items, int TotalItems)> GetPageAsync(
        int page, int pageSize, string? externalId, JobState? state,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Job> query = DbContext.Set<Job>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(externalId))
            query = query.Where(job => job.ExternalId == externalId.Trim());
        if (state.HasValue)
            query = query.Where(job => job.State == state.Value);

        int total = await query.CountAsync(cancellationToken);
        List<Job> items = await query.OrderByDescending(job => job.CreatedOnUtc)
            .ThenBy(job => job.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }
}
