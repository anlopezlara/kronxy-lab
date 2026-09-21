using System;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Jobs;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class JobLimitTests
{
	private static readonly DateTime UtcNow = new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void SameStateTransition_IsIdempotent()
	{
		Job job = CreateJob(3);
		Result result = job.TransitionTo(JobState.ContextBuilding, UtcNow, "start", "orchestrator", "corr");
		int count = job.Transitions.Count;
		Result result2 = job.TransitionTo(JobState.ContextBuilding, UtcNow, "start repeated", "orchestrator", "corr");
		Assert.True(result.IsSuccess);
		Assert.True(result2.IsSuccess);
		Assert.Equal<int>(count, job.Transitions.Count);
	}

	[Fact]
	public void RetryPending_AtMaxAttempts_IsRejected()
	{
		Job job = CreateJob(1);
		job.TransitionTo(JobState.ContextBuilding, UtcNow, "start", "orchestrator", "corr");
		Result result = job.TransitionTo(JobState.RetryPending, UtcNow, "retry", "orchestrator", "corr");
		Assert.True(result.IsFailure);
		Assert.Equal<Error>(JobErrors.MaxAttemptsExceeded, result.Error);
		Assert.Equal<int>(1, job.AttemptCount);
		Assert.Equal<JobState>(JobState.ContextBuilding, job.State);
	}

	[Fact]
	public void Active_stage_uses_configured_inactivity_duration()
	{
		Job job = CreateJob(3, TimeSpan.FromMinutes(30.0));
		job.TransitionTo(JobState.ContextBuilding, UtcNow, "start", "orchestrator", "corr");
		Assert.False(job.HasTimedOut(UtcNow.AddMinutes(29.0)));
		Assert.True(job.HasTimedOut(UtcNow.AddMinutes(30.0)));
	}

	[Fact]
	public void Old_job_with_recent_active_transition_does_not_time_out()
	{
		Job job = CreateJob(3, TimeSpan.FromMinutes(30.0));
		DateTime recentProgress = UtcNow.AddDays(3.0);
		job.TransitionTo(JobState.ContextBuilding, recentProgress, "start", "orchestrator", "corr");

		Assert.False(job.HasTimedOut(recentProgress.AddMinutes(29.0)));
	}

	[Fact]
	public void Recent_transition_restarts_timeout_reference()
	{
		Job job = CreateJob(3, TimeSpan.FromMinutes(30.0));
		job.TransitionTo(JobState.ContextBuilding, UtcNow, "context", "orchestrator", "corr-1");
		job.TransitionTo(JobState.Planning, UtcNow.AddMinutes(29.0), "planning", "orchestrator", "corr-2");

		Assert.False(job.HasTimedOut(UtcNow.AddMinutes(58.0)));
		Assert.True(job.HasTimedOut(UtcNow.AddMinutes(59.0)));
	}

	[Fact]
	public void Terminal_job_never_times_out_again()
	{
		Job job = CreateJob(3, TimeSpan.FromMinutes(30.0));
		job.TransitionTo(JobState.Cancelled, UtcNow, "cancel", "human", "corr");

		Assert.False(job.HasTimedOut(UtcNow.AddDays(30.0)));
	}

	[Fact]
	public void Waiting_human_does_not_time_out_by_age()
	{
		Job job = CreateJob(3, TimeSpan.FromMinutes(30.0));
		job.TransitionTo(JobState.ContextBuilding, UtcNow, "context", "orchestrator", "corr-1");
		job.TransitionTo(JobState.Planning, UtcNow, "planning", "orchestrator", "corr-2");
		job.TransitionTo(JobState.WorkspacePreparing, UtcNow, "workspace", "orchestrator", "corr-3");
		job.TransitionTo(JobState.Developing, UtcNow, "develop", "orchestrator", "corr-4");
		job.TransitionTo(JobState.Building, UtcNow, "build", "orchestrator", "corr-5");
		job.TransitionTo(JobState.Testing, UtcNow, "test", "orchestrator", "corr-6");
		job.TransitionTo(JobState.Reviewing, UtcNow, "review", "orchestrator", "corr-7");
		job.TransitionTo(JobState.WaitingHuman, UtcNow, "human review", "orchestrator", "corr-8");

		Assert.False(job.HasTimedOut(UtcNow.AddDays(30.0)));
	}

	[Fact]
	public void Created_job_does_not_time_out_before_execution_starts()
	{
		Job job = CreateJob(3, TimeSpan.FromMinutes(30.0));

		Assert.False(job.HasTimedOut(UtcNow.AddDays(30.0)));
	}

	private static Job CreateJob(int maxAttempts, TimeSpan? maxDuration = null)
	{
		JobLimits value = JobLimits.Create(maxDuration ?? TimeSpan.FromHours(2.0), maxAttempts, 10, 20).Value;
		return Job.Create(Guid.NewGuid(), "KRX-TEST", "test request", value, UtcNow).Value;
	}
}
