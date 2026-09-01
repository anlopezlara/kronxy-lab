using System;
using System.Threading;
using System.Threading.Tasks;
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Jobs;

namespace Kronxy.Application.Jobs;

public sealed class JobService : IJobService
{
	private readonly IJobRepository _jobRepository;

	private readonly IUnitOfWork _unitOfWork;

	private readonly IDateTimeProvider _clock;

	private readonly IJobIdGenerator _idGenerator;

	private readonly JobControlOptions _options;

	public JobService(IJobRepository jobRepository, IUnitOfWork unitOfWork, IDateTimeProvider clock, IJobIdGenerator idGenerator, JobControlOptions options)
	{
		_jobRepository = jobRepository;
		_unitOfWork = unitOfWork;
		_clock = clock;
		_idGenerator = idGenerator;
		_options = options;
	}

	public async Task<Result<Job>> CreateAsync(string request, string? externalId = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (!_options.TryCreateLimits(out JobLimits limits) || (object)limits == null)
		{
			return Result.Failure<Job>(JobApplicationErrors.InvalidConfiguration);
		}
		string normalizedExternalId = (string.IsNullOrWhiteSpace(externalId) ? _idGenerator.NewExternalId() : externalId.Trim());
		Job existing = await _jobRepository.GetByExternalIdAsync(normalizedExternalId, cancellationToken);
		if (existing != null)
		{
			return existing;
		}
		Result<Job> jobResult = Job.Create(_idGenerator.NewId(), normalizedExternalId, request, limits, _clock.UtcNow);
		if (jobResult.IsFailure)
		{
			return jobResult;
		}
		_jobRepository.Add(jobResult.Value);
		await _unitOfWork.SaveChangesAsync(cancellationToken);
		return jobResult.Value;
	}

	public async Task<Result<Job>> GetAsync(Guid jobId, CancellationToken cancellationToken = default(CancellationToken))
	{
		Job job = await _jobRepository.GetByIdAsync(jobId, cancellationToken);
		return (job == null) ? Result.Failure<Job>(JobApplicationErrors.NotFound) : ((Result<Job>)job);
	}

	public async Task<JobOperationResult> CancelAsync(Guid jobId, string actor, string correlationId, CancellationToken cancellationToken = default(CancellationToken))
	{
		Job job = await _jobRepository.GetByIdAsync(jobId, cancellationToken);
		if (job == null)
		{
			return JobOperationResult.Failure(JobOperationKind.PermanentFailure, JobApplicationErrors.NotFound);
		}
		if (job.State == JobState.Cancelled)
		{
			return JobOperationResult.Success();
		}
		if (job.IsTerminal)
		{
			return JobOperationResult.Failure(JobOperationKind.InvalidTransition, JobApplicationErrors.TerminalJob);
		}
		Result result = job.TransitionTo(JobState.Cancelled, _clock.UtcNow, "Job cancelled.", actor, correlationId);
		if (result.IsFailure)
		{
			return JobOperationResult.Failure(JobOperationKind.InvalidTransition, result.Error);
		}
		await _unitOfWork.SaveChangesAsync(cancellationToken);
		return JobOperationResult.Success();
	}
}
