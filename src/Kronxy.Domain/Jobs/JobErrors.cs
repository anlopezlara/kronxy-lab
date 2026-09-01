using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Jobs;

public static class JobErrors
{
	public static readonly Error InvalidRequest = new Error("Job.InvalidRequest", "The job request must not be empty.");

	public static readonly Error InvalidLimits = new Error("Job.InvalidLimits", "The job limits are invalid.");

	public static readonly Error TerminalState = new Error("Job.TerminalState", "A terminal job can not transition to another state.");

	public static readonly Error MaxAttemptsExceeded = new Error("Job.MaxAttemptsExceeded", "The maximum number of job attempts has been reached.");

	public static Error InvalidTransition(JobState from, JobState to)
	{
		return new Error("Job.InvalidTransition", $"Transition from {from} to {to} is not allowed.");
	}

        public static readonly Error InvalidBaseRepositoryHead =
                new Error(
                        "Job.InvalidBaseRepositoryHead",
                        "The base repository revision is invalid.");

        public static readonly Error BaseRepositoryHeadAlreadyPinned =
                new Error(
                        "Job.BaseRepositoryHeadAlreadyPinned",
                        "The base repository revision is already pinned to a different commit.");

}
