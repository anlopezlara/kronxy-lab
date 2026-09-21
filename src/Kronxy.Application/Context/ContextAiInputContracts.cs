namespace Kronxy.Application.Context;

public enum ContextAiInputFailureKind
{
    None = 0,
    InvalidRequest = 10,
    InvalidPackage = 20,
    PackageTooLarge = 30,
    EntryTooLarge = 40,
    ContentTooLarge = 50,
    InvalidEncoding = 60,
    Cancelled = 70,
    InternalFailure = 80
}

public sealed record ContextAiInputRequest
{
    public required ReadOnlyMemory<byte> PackageContent { get; init; }

    public required int MaxCharacters { get; init; }

    public IReadOnlyList<string> PriorityPaths { get; init; } = [];

    public IReadOnlyList<string> AllowedPathPrefixes { get; init; } = [];
}

public sealed record ContextAiInputResult(
    string Content,
    ContextAiInputFailureKind FailureKind,
    string ErrorCode)
{
    public bool IsSuccess =>
        FailureKind == ContextAiInputFailureKind.None;

    public static ContextAiInputResult Success(
        string content) =>
        new(
            content,
            ContextAiInputFailureKind.None,
            string.Empty);

    public static ContextAiInputResult Failure(
        ContextAiInputFailureKind kind,
        string errorCode) =>
        new(
            string.Empty,
            kind,
            errorCode);
}

public interface IContextAiInputBuilder
{
    Task<ContextAiInputResult> BuildAsync(
        ContextAiInputRequest request,
        CancellationToken cancellationToken = default);
}
