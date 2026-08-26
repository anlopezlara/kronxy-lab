namespace Kronxy.Context.Git;

public sealed record WorkingTreeEntry
{
    public required string Path { get; init; }
    public string? OriginalPath { get; init; }
    public GitFileState IndexState { get; init; }
    public GitFileState WorkTreeState { get; init; }
    public bool IsUntracked { get; init; }
    public bool IsConflict { get; init; }
    public bool IsRename => IndexState == GitFileState.Renamed || WorkTreeState == GitFileState.Renamed;
    public bool IsCopy => IndexState == GitFileState.Copied || WorkTreeState == GitFileState.Copied;
}
