using Kronxy.Domain.ProjectTasks;
namespace Kronxy.Infrastructure.Repositories;
internal sealed class ProjectTaskRepository : Repository<ProjectTask>, IProjectTaskRepository
{
    public ProjectTaskRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }
    public void Remove(ProjectTask projectTask)
    {
        DbContext.Remove(projectTask);
    }
}
