using Kronxy.Domain.Jobs;

namespace Kronxy.Application.Jobs;

public sealed class JobStateMachine : IJobStateMachine
{
	public JobState? DetermineNextAutomaticState(JobState currentState)
	{
		return JobWorkflow.DetermineNextAutomaticState(currentState);
	}

	public bool IsTerminal(JobState state)
	{
		return JobWorkflow.IsTerminalState(state);
	}

	public bool IsOperational(JobState state)
	{
		return JobWorkflow.IsOperationalState(state);
	}
}
