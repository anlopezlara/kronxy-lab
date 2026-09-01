namespace Kronxy.Application.Execution;

public sealed record SourceRevisionResult(
    string? Head,
    string ErrorCode)
{
    public bool IsSuccess =>
        !string.IsNullOrWhiteSpace(Head) &&
        string.IsNullOrWhiteSpace(ErrorCode);

    public static SourceRevisionResult Success(
        string head) =>
        new(head, string.Empty);

    public static SourceRevisionResult Failure(
        string errorCode) =>
        new(null, errorCode);
}

public interface ISourceRevisionProvider
{
    Task<SourceRevisionResult>
        GetAuthoritativeHeadAsync(
            CancellationToken cancellationToken = default);
}
