using Kronxy.Domain.Abstractions;

namespace Kronxy.Application.Jobs;

public sealed record JobOperationResult(JobOperationKind Kind, Error Error)
{
	public bool IsSuccess => Kind == JobOperationKind.Success;

	public static JobOperationResult Success()
	{
		return new JobOperationResult(JobOperationKind.Success, Kronxy.Domain.Abstractions.Error.None);
	}

	public static JobOperationResult Failure(JobOperationKind kind, Error error)
	{
		return new JobOperationResult(kind, error);
	}
}
