using Kronxy.Application.Execution;

namespace Kronxy.Infrastructure.Git;

public sealed class GitSourceRevisionProvider :
    ISourceRevisionProvider
{
    private const int MaximumOutputBytes =
        1024;

    private static readonly TimeSpan Timeout =
        TimeSpan.FromSeconds(15);

    private readonly string repositoryRoot;

    private readonly ISecureGitExecutor
        secureGitExecutor;

    public GitSourceRevisionProvider(
        string repositoryRoot,
        string gitExecutable)
    {
        if (string.IsNullOrWhiteSpace(
                repositoryRoot))
        {
            throw new ArgumentException(
                "Repository root is required.",
                nameof(repositoryRoot));
        }

        repositoryRoot =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(
                    repositoryRoot));

        if (!Directory.Exists(
                repositoryRoot))
        {
            throw new DirectoryNotFoundException(
                repositoryRoot);
        }

        this.repositoryRoot =
            repositoryRoot;

        secureGitExecutor =
            new SecureGitExecutor(
                gitExecutable);
    }

    public async Task<SourceRevisionResult>
        GetAuthoritativeHeadAsync(
            CancellationToken cancellationToken =
                default)
    {
        SecureGitResult result =
            await secureGitExecutor
                .ExecuteAsync(
                    new SecureGitRequest
                    {
                        Operation =
                            SecureGitOperation
                                .ResolveCommit,

                        WorkingDirectory =
                            repositoryRoot,

                        Reference =
                            "HEAD",

                        Timeout =
                            Timeout,

                        StandardOutputLimitBytes =
                            MaximumOutputBytes,

                        StandardErrorLimitBytes =
                            MaximumOutputBytes
                    },
                    cancellationToken)
                .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return SourceRevisionResult.Failure(
                MapFailure(result));
        }

        string head =
            result.StandardOutput
                .TrimEnd(
                    '\r',
                    '\n');

        if (!IsObjectId(head))
        {
            return SourceRevisionResult.Failure(
                "SOURCE_GIT_INVALID_HEAD");
        }

        return SourceRevisionResult.Success(
            head.ToLowerInvariant());
    }

    private static string MapFailure(
        SecureGitResult result)
    {
        return result.Outcome switch
        {
            SecureGitOutcome.Cancelled =>
                "SOURCE_GIT_CANCELLED",

            SecureGitOutcome.TimedOut =>
                "SOURCE_GIT_TIMED_OUT",

            SecureGitOutcome.OutputLimitExceeded =>
                "SOURCE_GIT_OUTPUT_LIMIT_EXCEEDED",

            SecureGitOutcome.StartFailure =>
                "SOURCE_GIT_START_FAILED",

            SecureGitOutcome.Rejected =>
                "SOURCE_GIT_REQUEST_REJECTED",

            SecureGitOutcome.NonZeroExitCode =>
                "SOURCE_GIT_HEAD_FAILED",

            _ =>
                "SOURCE_GIT_FAILED"
        };
    }

    private static bool IsObjectId(
        string value)
    {
        return
            value.Length is 40 or 64 &&
            value.All(
                Uri.IsHexDigit);
    }
}
