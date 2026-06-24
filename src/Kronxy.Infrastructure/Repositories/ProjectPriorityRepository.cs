using Kronxy.Domain.Projects;
using Microsoft.EntityFrameworkCore;
namespace Kronxy.Infrastructure.Repositories;
internal sealed class ProjectPriorityRepository : Repository<ProjectPriority>, IProjectPriorityRepository
{
    public ProjectPriorityRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }
    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        string normalizedCode = code.Trim().ToUpperInvariant();
        return await DbContext.Set<ProjectPriority>()
            .AnyAsync(projectPriority => projectPriority.Code == normalizedCode, cancellationToken);
    }
    public async Task<bool> ExistsByCodeAsync(string code, Guid excludeId, CancellationToken cancellationToken = default)
    {
        string normalizedCode = code.Trim().ToUpperInvariant();
        return await DbContext.Set<ProjectPriority>()
            .AnyAsync(projectPriority =>
                projectPriority.Code == normalizedCode &&
                projectPriority.Id != excludeId,
                cancellationToken);
    }
    public async Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<ProjectPriority>()
            .AnyAsync(projectPriority =>
                projectPriority.Id == id &&
                projectPriority.IsActive,
                cancellationToken);
    }
}
