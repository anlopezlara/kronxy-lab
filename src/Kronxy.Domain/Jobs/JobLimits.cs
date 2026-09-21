using System;
using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Jobs;

public sealed record JobLimits
{
	public TimeSpan MaxJobDuration { get; }

	public int MaxAttempts { get; }

	public int MaxDevelopmentAttempts => MaxAttempts;

	public int MaxAgentIterations { get; }

	public int MaxAiCalls { get; }

	private JobLimits(TimeSpan maxJobDuration, int maxAttempts, int maxAgentIterations, int maxAiCalls)
	{
		MaxJobDuration = maxJobDuration;
		MaxAttempts = maxAttempts;
		MaxAgentIterations = maxAgentIterations;
		MaxAiCalls = maxAiCalls;
	}

	public static Result<JobLimits> Create(TimeSpan maxJobDuration, int maxAttempts, int maxAgentIterations, int maxAiCalls)
	{
		if (maxJobDuration <= TimeSpan.Zero || maxAttempts <= 0 || maxAgentIterations <= 0 || maxAiCalls <= 0)
		{
			return Result.Failure<JobLimits>(JobErrors.InvalidLimits);
		}
		return new JobLimits(maxJobDuration, maxAttempts, maxAgentIterations, maxAiCalls);
	}
}
