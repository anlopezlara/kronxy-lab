using System;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Jobs;

namespace Kronxy.Application.Jobs;

public sealed class JobControlOptions
{
	public static JobControlOptions SafeDefaults => new JobControlOptions
	{
		MaxJobDuration = TimeSpan.FromHours(2.0),
		MaxAttempts = 3,
		MaxAgentIterations = 10,
		MaxAiCalls = 20
	};

	public TimeSpan MaxJobDuration { get; init; }

	public int MaxAttempts { get; init; }

	public int MaxAgentIterations { get; init; }

	public int MaxAiCalls { get; init; }

	public bool TryCreateLimits(out JobLimits? limits)
	{
		Result<JobLimits> result = JobLimits.Create(MaxJobDuration, MaxAttempts, MaxAgentIterations, MaxAiCalls);
		limits = (result.IsSuccess ? result.Value : null);
		return result.IsSuccess;
	}
}
