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
}
