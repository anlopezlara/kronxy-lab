namespace Kronxy.Context.Git;

public sealed record GitIndexEntry
{
    public required string Path { get; init; }
    public required string Mode { get; init; }
    public required string ObjectId { get; init; }
    public required int Stage { get; init; }
    public required GitIndexEntryType EntryType { get; init; }
    public bool IsConflict => Stage != 0;
    public bool IsSymbolicLink => EntryType == GitIndexEntryType.SymbolicLink;
    public bool IsGitLink => EntryType == GitIndexEntryType.GitLink;
    public bool IsExecutable => EntryType == GitIndexEntryType.ExecutableFile;
}
