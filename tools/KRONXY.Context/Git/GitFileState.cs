namespace Kronxy.Context.Git;

public enum GitFileState
{
    Unmodified,
    Modified,
    Added,
    Deleted,
    Renamed,
    Copied,
    TypeChanged,
    Unmerged,
    Unknown
}
