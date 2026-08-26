namespace Kronxy.Context.Git;

public sealed record RepositoryInfo
{
    public required string RootPath { get; init; }
    public required string HeadCommit { get; init; }
    public string? Branch { get; init; }
    public bool IsHeadDetached { get; init; }
}
