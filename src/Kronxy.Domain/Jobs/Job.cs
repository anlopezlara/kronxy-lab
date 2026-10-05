using System;
using System.Collections.Generic;
using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Jobs;

public sealed class Job : Entity
{
	private readonly List<JobTransition> _transitions = new List<JobTransition>();

	public string ExternalId { get; private set; } = string.Empty;

	public string Request { get; private set; } = string.Empty;

	public JobState State { get; private set; }

	public JobState? ResumeState { get; private set; }

	public JobLimits Limits { get; private set; } = null;

	public int AttemptCount { get; private set; }

	public DateTime CreatedOnUtc { get; private set; }

	public DateTime UpdatedOnUtc { get; private set; }

	public DateTime? ActiveExecutionStartedOnUtc { get; private set; }

	public DateTime? LastActiveProgressOnUtc { get; private set; }

	public DateTime? CompletedOnUtc { get; private set; }

	public string? BaseRepositoryHead { get; private set; }

	public string? LastErrorCode { get; private set; }

	public string? LastErrorMessage { get; private set; }

        public long Version { get; private set; }

	public IReadOnlyList<JobTransition> Transitions => _transitions.AsReadOnly();

	public bool IsTerminal => JobWorkflow.IsTerminalState(State);

	private Job()
	{
	}

	private Job(Guid id, string externalId, string request, JobLimits limits, DateTime createdOnUtc)
		: base(id)
	{
		ExternalId = externalId;
		Request = request;
		Limits = limits;
		State = JobState.Created;
		CreatedOnUtc = createdOnUtc;
		UpdatedOnUtc = createdOnUtc;
		AttemptCount = 1;
                Version = 1;
	}

	public bool HasTimedOut(DateTime utcNow)
	{
		return !IsTerminal &&
			JobWorkflow.IsOperationalState(State) &&
			ActiveExecutionStartedOnUtc.HasValue &&
			LastActiveProgressOnUtc.HasValue &&
			utcNow - LastActiveProgressOnUtc.Value >= Limits.MaxJobDuration;
	}

	public void BeginActiveExecution(DateTime utcNow)
	{
		if (ActiveExecutionStartedOnUtc.HasValue)
		{
			return;
		}

		ActiveExecutionStartedOnUtc = utcNow;
		LastActiveProgressOnUtc = utcNow;
	}

	public void RecordActiveProgress(DateTime utcNow)
	{
		if (!ActiveExecutionStartedOnUtc.HasValue)
		{
			return;
		}

		LastActiveProgressOnUtc = utcNow;
	}

	public void CompleteActiveExecution(DateTime utcNow)
	{
		if (!ActiveExecutionStartedOnUtc.HasValue)
		{
			return;
		}

		LastActiveProgressOnUtc = utcNow;
		ActiveExecutionStartedOnUtc = null;
	}

	public static Result<Job> Create(Guid id, string externalId, string request, JobLimits limits, DateTime utcNow)
	{
		if (id == Guid.Empty || string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(request))
		{
			return Result.Failure<Job>(JobErrors.InvalidRequest);
		}
		ArgumentNullException.ThrowIfNull(limits, "limits");
		return new Job(id, externalId.Trim(), request.Trim(), limits, utcNow);
	}

	public Result PinBaseRepositoryHead(string head)
	{
	        if (string.IsNullOrWhiteSpace(head))
	        {
	                return Result.Failure(JobErrors.InvalidBaseRepositoryHead);
	        }

	        string normalized = head.Trim().ToLowerInvariant();

	        if (normalized.Length != 40 && normalized.Length != 64)
	        {
	                return Result.Failure(JobErrors.InvalidBaseRepositoryHead);
	        }

	        foreach (char character in normalized)
	        {
	                if (!Uri.IsHexDigit(character))
	                {
	                        return Result.Failure(JobErrors.InvalidBaseRepositoryHead);
	                }
	        }

	        if (BaseRepositoryHead is null)
	        {
	                BaseRepositoryHead = normalized;
	                return Result.Success();
	        }

	        if (string.Equals(
	                BaseRepositoryHead,
	                normalized,
	                StringComparison.Ordinal))
	        {
	                return Result.Success();
	        }

	        return Result.Failure(
	                JobErrors.BaseRepositoryHeadAlreadyPinned);
	}

	public Result TransitionTo(JobState targetState, DateTime utcNow, string reason, string actor, string correlationId)
	{
		if (State == targetState)
		{
			return Result.Success();
		}
		if (IsTerminal)
		{
			return Result.Failure(JobErrors.TerminalState);
		}
		if (targetState == JobState.RetryPending && AttemptCount >= Limits.MaxAttempts)
		{
			return Result.Failure(JobErrors.MaxAttemptsExceeded);
		}
		if (!IsAllowedTransition(State, targetState))
		{
			return Result.Failure(JobErrors.InvalidTransition(State, targetState));
		}
		JobState state = State;
		if ((targetState == JobState.WaitingAi || targetState == JobState.RetryPending) ? true : false)
		{
			ResumeState = state;
			if (targetState == JobState.RetryPending)
			{
				AttemptCount++;
			}
		}
		else
		{
			JobState state2 = State;
			if ((state2 == JobState.WaitingAi || state2 == JobState.RetryPending) ? true : false)
			{
				ResumeState = null;
			}
		}
		State = targetState;
		UpdatedOnUtc = utcNow;
		RecordActiveProgress(utcNow);
		if (JobWorkflow.IsTerminalState(targetState))
		{
			CompletedOnUtc = utcNow;
			ResumeState = null;
		}
		_transitions.Add(new JobTransition(state, targetState, utcNow, reason ?? string.Empty, actor ?? string.Empty, correlationId ?? string.Empty));
		return Result.Success();
	}

	public Result BeginDevelopmentCorrection(DateTime utcNow, string reason, string actor, string correlationId)
	{
		if (State != JobState.Reviewing) return Result.Failure(JobErrors.InvalidTransition(State, JobState.Developing));
		if (AttemptCount >= Limits.MaxDevelopmentAttempts) return Result.Failure(JobErrors.MaxAttemptsExceeded);
		AttemptCount++;
		return TransitionTo(JobState.Developing, utcNow, reason, actor, correlationId);
	}

	public Result BeginHumanReviewCorrection(DateTime utcNow, string reason, string actor, string correlationId)
	{
		if (State != JobState.WaitingHuman)
		{
			return Result.Failure(JobErrors.InvalidTransition(State, JobState.Developing));
		}

		return TransitionTo(JobState.Developing, utcNow, reason, actor, correlationId);
	}

	public Result Resume(DateTime utcNow, string reason, string actor, string correlationId)
	{
		JobState state = State;
		bool flag = ((state == JobState.WaitingAi || state == JobState.RetryPending) ? true : false);
		if (!flag || !ResumeState.HasValue)
		{
			return Result.Failure(JobErrors.InvalidTransition(State, State));
		}
		JobState value = ResumeState.Value;
		return TransitionTo(value, utcNow, reason, actor, correlationId);
	}

	private bool IsAllowedTransition(JobState from, JobState to)
	{
		if ((from == JobState.WaitingAi || from == JobState.RetryPending) ? true : false)
		{
			return ResumeState == to;
		}
		if ((to == JobState.WaitingAi || to == JobState.RetryPending) ? true : false)
		{
			return JobWorkflow.IsOperationalState(from);
		}
		return JobWorkflow.IsBaseTransitionAllowed(from, to);
	}
}
