namespace Kronxy.Application.Jobs;

public enum JobOperationKind
{
	Success = 0,
	RetryableFailure = 10,
	PermanentFailure = 20,
	Cancelled = 30,
	TimedOut = 40,
	InvalidTransition = 50,
	OperationalFailure = 60
}
