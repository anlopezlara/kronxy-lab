namespace Kronxy.Context.Git;

public enum GitErrorKind
{
    GitUnavailable,
    PathNotFound,
    NotARepository,
    NoInitialCommit,
    InvalidReference,
    ReferenceNotFound,
    CommandFailed,
    TimedOut,
    OutputLimitExceeded,
    MalformedOutput
}
