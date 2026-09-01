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
	public void HasTimedOut_UsesConfiguredDuration()
	{
		Job job = CreateJob(3, TimeSpan.FromMinutes(30.0));
		Assert.False(job.HasTimedOut(UtcNow.AddMinutes(29.0)));
		Assert.True(job.HasTimedOut(UtcNow.AddMinutes(30.0)));
	}

	private static Job CreateJob(int maxAttempts, TimeSpan? maxDuration = null)
	{
		JobLimits value = JobLimits.Create(maxDuration ?? TimeSpan.FromHours(2.0), maxAttempts, 10, 20).Value;
		return Job.Create(Guid.NewGuid(), "KRX-TEST", "test request", value, UtcNow).Value;
	}
}
