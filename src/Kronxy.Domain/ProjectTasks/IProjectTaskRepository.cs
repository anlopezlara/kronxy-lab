namespace Kronxy.Domain.ProjectTasks;
public interface IProjectTaskRepository
{
    Task<ProjectTask?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    void Add(ProjectTask projectTask);
    void Remove(ProjectTask projectTask);
}
