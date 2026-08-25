namespace Kronxy.Context.Commands;

public static class ExitCodes
{
    public const int Success = 0;
    public const int InvalidArguments = 1;
    public const int GitUnavailable = 2;
    public const int InvalidGitReference = 3;
    public const int WorkingTreePolicyViolation = 4;
    public const int SensitiveContentDetected = 5;
    public const int PackageLimitExceeded = 6;
    public const int IoOrPackagingFailure = 7;
    public const int ExternalCommandFailure = 8;
    public const int InvalidPackage = 9;
    public const int IncompleteOperation = 10;
    public const int UnexpectedError = 70;
}
