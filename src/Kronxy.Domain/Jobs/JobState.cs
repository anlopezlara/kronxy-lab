namespace Kronxy.Domain.Jobs;

public enum JobState
{
	Created = 0,
	ContextBuilding = 10,
	Planning = 20,
	WorkspacePreparing = 30,
	Developing = 40,
	Building = 50,
	Testing = 60,
	Reviewing = 70,
	WaitingHuman = 80,
	Completed = 90,
	WaitingAi = 100,
	RetryPending = 110,
	Failed = 200,
	Rejected = 210,
	Cancelled = 220,
	TimedOut = 230,
	Interrupted = 240
}
