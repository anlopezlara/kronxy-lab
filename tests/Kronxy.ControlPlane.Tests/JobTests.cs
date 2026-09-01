using System;
using System.Collections;
using System.Collections.Generic;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Jobs;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class JobTests
{
	private static readonly DateTime UtcNow = new DateTime(2026, 8, 29, 10, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void Create_WithValidInput_CreatesJob()
	{
		Result<Job> result = CreateJob();
		Assert.True(result.IsSuccess);
		Assert.Equal<JobState>(JobState.Created, result.Value.State);
		Assert.False(result.Value.IsTerminal);
		Assert.Equal<int>(1, result.Value.AttemptCount);
		Assert.Empty((IEnumerable)result.Value.Transitions);
	}

	[Fact]
	public void Create_WithEmptyRequest_Fails()
	{
		JobLimits limits = CreateLimits();
		Result<Job> result = Job.Create(Guid.NewGuid(), "job-1", " ", limits, UtcNow);
		Assert.True(result.IsFailure);
		Assert.Equal<Error>(JobErrors.InvalidRequest, result.Error);
	}

	[Fact]
	public void JobLimits_WithInvalidValues_Fails()
	{
		Result<JobLimits> result = JobLimits.Create(TimeSpan.Zero, 0, 0, 0);
		Assert.True(result.IsFailure);
		Assert.Equal<Error>(JobErrors.InvalidLimits, result.Error);
	}

	[Fact]
	public void NormalFlow_ReachesCompleted()
	{
		Job value = CreateJob().Value;
		Transition(value, JobState.ContextBuilding);
		Transition(value, JobState.Planning);
		Transition(value, JobState.WorkspacePreparing);
		Transition(value, JobState.Developing);
		Transition(value, JobState.Building);
		Transition(value, JobState.Testing);
		Transition(value, JobState.Reviewing);
		Transition(value, JobState.WaitingHuman);
		Transition(value, JobState.Completed);
		Assert.Equal<JobState>(JobState.Completed, value.State);
		Assert.True(value.IsTerminal);
		Assert.NotNull<DateTime>(value.CompletedOnUtc);
		Assert.Equal<int>(9, value.Transitions.Count);
	}

	[Fact]
	public void IllegalJump_IsRejected()
	{
		Job value = CreateJob().Value;
		Result result = value.TransitionTo(JobState.Developing, UtcNow, "illegal", "test", "corr-1");
		Assert.True(result.IsFailure);
		Assert.Equal<JobState>(JobState.Created, value.State);
		Assert.Empty((IEnumerable)value.Transitions);
	}

	[Fact]
	public void TerminalJob_CannotTransitionAgain()
	{
		Job value = CreateJob().Value;
		Result result = value.TransitionTo(JobState.Cancelled, UtcNow, "cancel", "human", "corr-1");
		Assert.True(result.IsSuccess);
		Result result2 = value.TransitionTo(JobState.ContextBuilding, UtcNow.AddMinutes(1.0), "invalid", "test", "corr-2");
		Assert.True(result2.IsFailure);
		Assert.Equal<Error>(JobErrors.TerminalState, result2.Error);
		Assert.Equal<JobState>(JobState.Cancelled, value.State);
	}

	[Fact]
	public void WaitingHuman_CanBeRejected()
	{
		Job job = CreateAtWaitingHuman();
		Result result = job.TransitionTo(JobState.Rejected, UtcNow.AddMinutes(10.0), "human rejected", "human", "corr-reject");
		Assert.True(result.IsSuccess);
		Assert.Equal<JobState>(JobState.Rejected, job.State);
		Assert.True(job.IsTerminal);
	}

	[Theory]
	[InlineData(new object[] { JobState.Failed })]
	[InlineData(new object[] { JobState.Cancelled })]
	[InlineData(new object[] { JobState.TimedOut })]
	[InlineData(new object[] { JobState.Interrupted })]
	public void OperationalJob_CanEnterTerminalFailureState(JobState terminalState)
	{
		Job value = CreateJob().Value;
		Transition(value, JobState.ContextBuilding);
		Result result = value.TransitionTo(terminalState, UtcNow.AddMinutes(1.0), "stop", "control-plane", "corr-stop");
		Assert.True(result.IsSuccess);
		Assert.Equal<JobState>(terminalState, value.State);
		Assert.True(value.IsTerminal);
	}

	[Fact]
	public void Transition_RecordsAuditData()
	{
		Job value = CreateJob().Value;
		DateTime dateTime = UtcNow.AddMinutes(1.0);
		Result result = value.TransitionTo(JobState.ContextBuilding, dateTime, "context requested", "orchestrator", "corr-123");
		Assert.True(result.IsSuccess);
		JobTransition jobTransition = Assert.Single<JobTransition>((IEnumerable<JobTransition>)value.Transitions);
		Assert.Equal<JobState>(JobState.Created, jobTransition.FromState);
		Assert.Equal<JobState>(JobState.ContextBuilding, jobTransition.ToState);
		Assert.Equal(dateTime, jobTransition.OccurredOnUtc);
		Assert.Equal("context requested", jobTransition.Reason);
		Assert.Equal("orchestrator", jobTransition.Actor);
		Assert.Equal("corr-123", jobTransition.CorrelationId);
	}

	[Fact]
	public void OperationalJob_CanWaitForAi_AndResumeSameState()
	{
		Job value = CreateJob().Value;
		Transition(value, JobState.ContextBuilding);
		Transition(value, JobState.Planning);
		Result result = value.TransitionTo(JobState.WaitingAi, UtcNow.AddMinutes(2.0), "ai unavailable", "orchestrator", "corr-ai");
		Assert.True(result.IsSuccess);
		Assert.Equal<JobState>(JobState.WaitingAi, value.State);
		Assert.Equal<JobState?>((JobState?)JobState.Planning, value.ResumeState);
		Result result2 = value.Resume(UtcNow.AddMinutes(3.0), "ai available", "orchestrator", "corr-ai");
		Assert.True(result2.IsSuccess);
		Assert.Equal<JobState>(JobState.Planning, value.State);
		Assert.Null<JobState>(value.ResumeState);
	}

	[Fact]
	public void OperationalJob_CanEnterRetryPending_AndResumeSameState()
	{
		Job value = CreateJob().Value;
		Transition(value, JobState.ContextBuilding);
		Transition(value, JobState.Planning);
		Transition(value, JobState.WorkspacePreparing);
		Transition(value, JobState.Developing);
		Transition(value, JobState.Building);
		Result result = value.TransitionTo(JobState.RetryPending, UtcNow.AddMinutes(5.0), "build transient failure", "orchestrator", "corr-retry");
		Assert.True(result.IsSuccess);
		Assert.Equal<JobState?>((JobState?)JobState.Building, value.ResumeState);
		Result result2 = value.Resume(UtcNow.AddMinutes(6.0), "retry", "orchestrator", "corr-retry");
		Assert.True(result2.IsSuccess);
		Assert.Equal<JobState>(JobState.Building, value.State);
		Assert.Null<JobState>(value.ResumeState);
	}

	[Fact]
	public void Resume_WhenNotPaused_IsRejected()
	{
		Job value = CreateJob().Value;
		Result result = value.Resume(UtcNow, "invalid resume", "test", "corr");
		Assert.True(result.IsFailure);
		Assert.Equal<JobState>(JobState.Created, value.State);
	}

	private static Result<Job> CreateJob()
	{
		return Job.Create(Guid.NewGuid(), "job-001", "Implement deterministic control plane", CreateLimits(), UtcNow);
	}

	private static JobLimits CreateLimits()
	{
		return JobLimits.Create(TimeSpan.FromHours(2.0), 3, 10, 20).Value;
	}

	private static Job CreateAtWaitingHuman()
	{
		Job value = CreateJob().Value;
		Transition(value, JobState.ContextBuilding);
		Transition(value, JobState.Planning);
		Transition(value, JobState.WorkspacePreparing);
		Transition(value, JobState.Developing);
		Transition(value, JobState.Building);
		Transition(value, JobState.Testing);
		Transition(value, JobState.Reviewing);
		Transition(value, JobState.WaitingHuman);
		return value;
	}

	private static void Transition(Job job, JobState state)
	{
		Result result = job.TransitionTo(state, UtcNow, $"to {state}", "test", "corr");
		Assert.True(result.IsSuccess, $"Expected transition to {state} to succeed. Error: {result.Error.Code}");
	}
        [Fact]
        public void BaseRepositoryHead_CanBePinnedOnce()
        {
                Result<Job> result =
                        Job.Create(
                                Guid.NewGuid(),
                                "KRX-900001",
                                "test",
                                CreateLimits(),
                                UtcNow);

                Assert.True(result.IsSuccess);

                Job job = result.Value;

                const string head =
                        "0123456789abcdef0123456789abcdef01234567";

                Result pin =
                        job.PinBaseRepositoryHead(head);

                Assert.True(pin.IsSuccess);

                Assert.Equal(
                        head,
                        job.BaseRepositoryHead);
        }

        [Fact]
        public void BaseRepositoryHead_RepeatedSameCommit_IsIdempotent()
        {
                Result<Job> result =
                        Job.Create(
                                Guid.NewGuid(),
                                "KRX-900002",
                                "test",
                                CreateLimits(),
                                UtcNow);

                Assert.True(result.IsSuccess);

                Job job = result.Value;

                const string upper =
                        "ABCDEF0123456789ABCDEF0123456789ABCDEF01";

                const string lower =
                        "abcdef0123456789abcdef0123456789abcdef01";

                Result first =
                        job.PinBaseRepositoryHead(upper);

                Result second =
                        job.PinBaseRepositoryHead(lower);

                Assert.True(first.IsSuccess);
                Assert.True(second.IsSuccess);

                Assert.Equal(
                        lower,
                        job.BaseRepositoryHead);
        }

        [Fact]
        public void BaseRepositoryHead_DifferentCommit_IsRejected()
        {
                Result<Job> result =
                        Job.Create(
                                Guid.NewGuid(),
                                "KRX-900003",
                                "test",
                                CreateLimits(),
                                UtcNow);

                Assert.True(result.IsSuccess);

                Job job = result.Value;

                const string first =
                        "1111111111111111111111111111111111111111";

                const string second =
                        "2222222222222222222222222222222222222222";

                Assert.True(
                        job.PinBaseRepositoryHead(first)
                            .IsSuccess);

                Result secondPin =
                        job.PinBaseRepositoryHead(second);

                Assert.True(secondPin.IsFailure);

                Assert.Equal(
                        JobErrors.BaseRepositoryHeadAlreadyPinned,
                        secondPin.Error);

                Assert.Equal(
                        first,
                        job.BaseRepositoryHead);
        }

        [Theory]
        [InlineData("")]
        [InlineData("HEAD")]
        [InlineData("../HEAD")]
        [InlineData("1234")]
        [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
        public void BaseRepositoryHead_InvalidValue_IsRejected(
                string head)
        {
                Result<Job> result =
                        Job.Create(
                                Guid.NewGuid(),
                                "KRX-900004",
                                "test",
                                CreateLimits(),
                                UtcNow);

                Assert.True(result.IsSuccess);

                Job job = result.Value;

                Result pin =
                        job.PinBaseRepositoryHead(head);

                Assert.True(pin.IsFailure);

                Assert.Equal(
                        JobErrors.InvalidBaseRepositoryHead,
                        pin.Error);

                Assert.Null(
                        job.BaseRepositoryHead);
        }


}
