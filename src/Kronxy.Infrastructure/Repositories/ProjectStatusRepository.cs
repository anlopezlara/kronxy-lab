using Kronxy.Domain.Projects;
using Microsoft.EntityFrameworkCore;
namespace Kronxy.Infrastructure.Repositories;
internal sealed class ProjectStatusRepository : Repository<ProjectStatus>, IProjectStatusRepository
{
    public ProjectStatusRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }
    public async Task<bool> ExistsByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        string normalizedCode = code.Trim().ToUpperInvariant();
        return await DbContext
            .Set<ProjectStatus>()
            .AnyAsync(projectStatus => projectStatus.Code == normalizedCode, cancellationToken);
    }
    public async Task<bool> ExistsByCodeAsync(
        string code,
        Guid excludeId,
        CancellationToken cancellationToken = default)
    {
        string normalizedCode = code.Trim().ToUpperInvariant();
        return await DbContext
            .Set<ProjectStatus>()
            .AnyAsync(
                projectStatus =>
                    projectStatus.Code == normalizedCode &&
                    projectStatus.Id != excludeId,
                cancellationToken);
    }
    public async Task<bool> IsActiveAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<ProjectStatus>()
            .AnyAsync(
                projectStatus =>
                    projectStatus.Id == id &&
                    projectStatus.IsActive,
                cancellationToken);
    }
}
