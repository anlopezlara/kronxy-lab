using Kronxy.Application.Jobs;
using Kronxy.Domain.Jobs;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class JobStateMachineTests
{
	private readonly IJobStateMachine _stateMachine = new JobStateMachine();

	[Theory]
	[InlineData(new object[]
	{
		JobState.Created,
		JobState.ContextBuilding
	})]
	[InlineData(new object[]
	{
		JobState.ContextBuilding,
		JobState.Planning
	})]
	[InlineData(new object[]
	{
		JobState.Planning,
		JobState.WorkspacePreparing
	})]
	[InlineData(new object[]
	{
		JobState.WorkspacePreparing,
		JobState.Developing
	})]
	[InlineData(new object[]
	{
		JobState.Developing,
		JobState.Building
	})]
	[InlineData(new object[]
	{
		JobState.Building,
		JobState.Testing
	})]
	[InlineData(new object[]
	{
		JobState.Testing,
		JobState.Reviewing
	})]
	[InlineData(new object[]
	{
		JobState.Reviewing,
		JobState.WaitingHuman
	})]
	public void AutomaticFlow_IsDeterministic(JobState current, JobState expected)
	{
		Assert.Equal<JobState?>((JobState?)expected, _stateMachine.DetermineNextAutomaticState(current));
		Assert.Equal<JobState?>((JobState?)expected, _stateMachine.DetermineNextAutomaticState(current));
	}

	[Theory]
	[InlineData(new object[] { JobState.WaitingHuman })]
	[InlineData(new object[] { JobState.WaitingAi })]
	[InlineData(new object[] { JobState.RetryPending })]
	[InlineData(new object[] { JobState.Completed })]
	[InlineData(new object[] { JobState.Failed })]
	[InlineData(new object[] { JobState.Rejected })]
	[InlineData(new object[] { JobState.Cancelled })]
	[InlineData(new object[] { JobState.TimedOut })]
	[InlineData(new object[] { JobState.Interrupted })]
	public void NonAutomaticStates_HaveNoNextState(JobState state)
	{
		Assert.Null<JobState>(_stateMachine.DetermineNextAutomaticState(state));
	}

	[Theory]
	[InlineData(new object[] { JobState.Completed })]
	[InlineData(new object[] { JobState.Failed })]
	[InlineData(new object[] { JobState.Rejected })]
	[InlineData(new object[] { JobState.Cancelled })]
	[InlineData(new object[] { JobState.TimedOut })]
	[InlineData(new object[] { JobState.Interrupted })]
	public void TerminalStates_AreRecognized(JobState state)
	{
		Assert.True(_stateMachine.IsTerminal(state));
	}

	[Theory]
	[InlineData(new object[] { JobState.ContextBuilding })]
	[InlineData(new object[] { JobState.Planning })]
	[InlineData(new object[] { JobState.WorkspacePreparing })]
	[InlineData(new object[] { JobState.Developing })]
	[InlineData(new object[] { JobState.Building })]
	[InlineData(new object[] { JobState.Testing })]
	[InlineData(new object[] { JobState.Reviewing })]
	public void OperationalStates_AreRecognized(JobState state)
	{
		Assert.True(_stateMachine.IsOperational(state));
	}
}
