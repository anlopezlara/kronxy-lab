using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kronxy.Domain.Jobs;

public interface IJobRepository
{
	Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default(CancellationToken));

	Task<Job?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default(CancellationToken));

	void Add(Job job);
}
