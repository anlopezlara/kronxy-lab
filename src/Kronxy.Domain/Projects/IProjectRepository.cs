namespace Kronxy.Domain.Projects;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Project?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    void Add(Project project);

    void Remove(Project project);
}