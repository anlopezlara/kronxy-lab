using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kronxy.Domain.Jobs;

public interface IJobRepository
{
	Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default(CancellationToken));

	Task<Job?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default(CancellationToken));

	Task<(IReadOnlyList<Job> Items, int TotalItems)> GetPageAsync(
		int page, int pageSize, string? externalId, JobState? state,
		CancellationToken cancellationToken = default) =>
		Task.FromResult(((IReadOnlyList<Job>)Array.Empty<Job>(), 0));

	void Add(Job job);
}
