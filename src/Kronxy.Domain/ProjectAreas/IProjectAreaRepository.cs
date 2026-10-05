namespace Kronxy.Domain.ProjectAreas;

using System.Threading;
using System.Threading.Tasks;

public interface IProjectAreaRepository
{
    Task<ProjectArea?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Add(ProjectArea projectArea);
}
