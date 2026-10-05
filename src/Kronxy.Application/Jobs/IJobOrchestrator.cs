using System;
using System.Threading;
using System.Threading.Tasks;
using Kronxy.Application.Execution;
using Kronxy.Domain.Jobs;

namespace Kronxy.Application.Jobs;

public interface IJobOrchestrator
{
	Task<JobOperationResult> AdvanceAsync(Guid jobId, string actor, string correlationId, CancellationToken cancellationToken = default(CancellationToken));

	Task<JobOperationResult> MarkRetryPendingAsync(Guid jobId, string reason, string actor, string correlationId, CancellationToken cancellationToken = default(CancellationToken));

	Task<JobOperationResult> ResumeAsync(Guid jobId, string reason, string actor, string correlationId, CancellationToken cancellationToken = default(CancellationToken));

	Task<JobOperationResult> RequestHumanReviewCorrectionAsync(Guid jobId, HumanReviewCorrectionRequest request, CancellationToken cancellationToken = default(CancellationToken));

	Task<JobOperationResult> ApplyGovernedHumanCorrectionAsync(Guid jobId, GovernedHumanCorrectionRequest request, CancellationToken cancellationToken = default(CancellationToken));

	Task<JobOperationResult> SupersedeHumanReviewCorrectionReviewAsync(Guid jobId, string actor, string correlationId, CancellationToken cancellationToken = default(CancellationToken));

	Task<JobOperationResult> ApproveHumanReviewAsync(Guid jobId, string actor, string correlationId, CancellationToken cancellationToken = default(CancellationToken));

	Task<JobOperationResult> ResolveArchitectureDecisionAsync(Guid jobId, ArchitectureDecisionRequest request, CancellationToken cancellationToken = default(CancellationToken));

	JobState? DetermineNextState(JobState currentState);
}
