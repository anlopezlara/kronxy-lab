using Kronxy.Domain.Projects;
using Microsoft.EntityFrameworkCore;
namespace Kronxy.Infrastructure.Repositories;
internal sealed class ProjectTypeRepository : Repository<ProjectType>, IProjectTypeRepository
{
    public ProjectTypeRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }
    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        string normalizedCode = code.Trim().ToUpperInvariant();
        return await DbContext.Set<ProjectType>()
            .AnyAsync(projectType => projectType.Code == normalizedCode, cancellationToken);
    }
    public async Task<bool> ExistsByCodeAsync(string code, Guid excludeId, CancellationToken cancellationToken = default)
    {
        string normalizedCode = code.Trim().ToUpperInvariant();
        return await DbContext.Set<ProjectType>()
            .AnyAsync(projectType =>
                projectType.Code == normalizedCode &&
                projectType.Id != excludeId,
                cancellationToken);
    }
    public async Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<ProjectType>()
            .AnyAsync(projectType =>
                projectType.Id == id &&
                projectType.IsActive,
                cancellationToken);
    }
}
