using System;

namespace Kronxy.Domain.Jobs;

public sealed record JobTransition(JobState FromState, JobState ToState, DateTime OccurredOnUtc, string Reason, string Actor, string CorrelationId);
