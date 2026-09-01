using System;
using System.Threading;
using System.Threading.Tasks;
using Kronxy.Domain.Jobs;

namespace Kronxy.Application.Jobs;

public interface IJobOrchestrator
{
	Task<JobOperationResult> AdvanceAsync(Guid jobId, string actor, string correlationId, CancellationToken cancellationToken = default(CancellationToken));

	Task<JobOperationResult> MarkRetryPendingAsync(Guid jobId, string reason, string actor, string correlationId, CancellationToken cancellationToken = default(CancellationToken));

	Task<JobOperationResult> ResumeAsync(Guid jobId, string reason, string actor, string correlationId, CancellationToken cancellationToken = default(CancellationToken));

	JobState? DetermineNextState(JobState currentState);
}
