namespace Kronxy.Domain.ProjectModules;

using System.Threading;
using System.Threading.Tasks;

public interface IProjectModuleRepository
{
    Task<ProjectModule?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    void Add(ProjectModule projectModule);
}
