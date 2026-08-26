namespace Kronxy.Context.Git;

public sealed record WorkingTreeStatus
{
    public IReadOnlyList<WorkingTreeEntry> Entries { get; init; } = [];
    public bool IsClean => Entries.Count == 0;
    public bool HasStagedChanges => Entries.Any(entry =>
        !entry.IsUntracked && entry.IndexState != GitFileState.Unmodified);
    public bool HasUnstagedChanges => Entries.Any(entry =>
        !entry.IsUntracked && entry.WorkTreeState != GitFileState.Unmodified);
    public bool HasUntrackedFiles => Entries.Any(entry => entry.IsUntracked);
    public bool HasConflicts => Entries.Any(entry => entry.IsConflict);
}
