using Kronxy.Context.Git;

namespace Kronxy.Context.Files;

public enum RepositoryFileOrigin { Tracked, Untracked }

public sealed record RepositoryFileCandidate
{
    public required string LogicalPath { get; init; }
    public required RepositoryFileOrigin Origin { get; init; }
    public GitIndexEntryType? GitEntryType { get; init; }
    public int Stage { get; init; }
    public bool IsConflict => Stage != 0;
    public bool IsSymbolicLink => GitEntryType == GitIndexEntryType.SymbolicLink;
    public bool IsGitLink => GitEntryType == GitIndexEntryType.GitLink;
    public bool IsExecutable => GitEntryType == GitIndexEntryType.ExecutableFile;
}

public enum FileExclusionReason
{
    None,
    ProhibitedDirectory,
    SensitiveName,
    BinaryExtension,
    SymbolicLink,
    GitLink,
    UnknownGitType,
    Conflict,
    InvalidPath,
    OutsideRepository,
    ReparsePoint,
    Directory,
    Missing,
    AccessDenied,
    TooLarge,
    Duplicate,
    CaseCollision,
    IoError
}

public sealed record FileSelectionDecision
{
    public required bool Include { get; init; }
    public FileExclusionReason Reason { get; init; }
}

public enum RepositoryInventoryStatus { Complete, CandidateLimitExceeded, AcceptedFileLimitExceeded, TotalSizeLimitExceeded }

public sealed record RepositoryInventoryFile
{
    public required string LogicalPath { get; init; }
    public required RepositoryFileOrigin Origin { get; init; }
    public required long SizeBytes { get; init; }
    public bool IsExecutable { get; init; }
}

public sealed record RepositoryExcludedFile
{
    public required string LogicalPath { get; init; }
    public required FileExclusionReason Reason { get; init; }
}

public sealed record RepositoryFileInventoryResult
{
    public RepositoryInventoryStatus Status { get; init; }
    public IReadOnlyList<RepositoryInventoryFile> Accepted { get; init; } = [];
    public IReadOnlyList<RepositoryExcludedFile> Excluded { get; init; } = [];
    public bool IsComplete => Status == RepositoryInventoryStatus.Complete;
}
