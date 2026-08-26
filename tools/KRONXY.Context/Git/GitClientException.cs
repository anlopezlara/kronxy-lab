namespace Kronxy.Context.Git;

public sealed class GitClientException : Exception
{
    public GitClientException(GitErrorKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    public GitErrorKind Kind { get; }
}
