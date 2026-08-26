namespace Kronxy.Context.Git;

public sealed record GitChange
{
    public required GitChangeType ChangeType { get; init; }
    public required string Path { get; init; }
    public string? OriginalPath { get; init; }
}
