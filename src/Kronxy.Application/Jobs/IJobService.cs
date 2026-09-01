using System;
using System.Threading;
using System.Threading.Tasks;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Jobs;

namespace Kronxy.Application.Jobs;

public interface IJobService
{
	Task<Result<Job>> CreateAsync(string request, string? externalId = null, CancellationToken cancellationToken = default(CancellationToken));

	Task<Result<Job>> GetAsync(Guid jobId, CancellationToken cancellationToken = default(CancellationToken));

	Task<JobOperationResult> CancelAsync(Guid jobId, string actor, string correlationId, CancellationToken cancellationToken = default(CancellationToken));
}
