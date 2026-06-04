using Kronxy.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Kronxy.Infrastructure.Repositories;

internal sealed class ProjectRepository : Repository<Project>, IProjectRepository
{
    public ProjectRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<Project?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<Project>()
            .FirstOrDefaultAsync(
                project => project.Code == code,
                cancellationToken);
    }

    public void Remove(Project project)
    {
        DbContext.Remove(project);
    }
}