using Kronxy.Domain.Jobs;

namespace Kronxy.Application.Jobs;

public interface IJobStateMachine
{
	JobState? DetermineNextAutomaticState(JobState currentState);

	bool IsTerminal(JobState state);

	bool IsOperational(JobState state);
}
