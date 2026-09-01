using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;
using Kronxy.Context.Git;

namespace Kronxy.Infrastructure.Context;

public sealed class SecureContextGitClient : IGitClient
{
    private readonly ISecureToolExecutor executor;
    private readonly RepositoryWorktreeHandle repository;
    private readonly string correlationId;

    public SecureContextGitClient(
        ISecureToolExecutor executor,
        RepositoryWorktreeHandle repository,
        string correlationId)
    {
        this.executor =
            executor ??
            throw new ArgumentNullException(
                nameof(executor));

        this.repository =
            repository ??
            throw new ArgumentNullException(
                nameof(repository));

        if (string.IsNullOrWhiteSpace(
                repository.RepositoryPath) ||
            string.IsNullOrWhiteSpace(
                repository.WorkspacePath) ||
            string.IsNullOrWhiteSpace(
                repository.Head))
        {
            throw new ArgumentException(
                "Repository worktree handle is invalid.",
                nameof(repository));
        }

        if (correlationId.IndexOfAny(
                ['\0', '\r', '\n']) >= 0)
        {
            throw new ArgumentException(
                "Correlation id is invalid.",
                nameof(correlationId));
        }

        this.correlationId =
            correlationId;
    }

    public Task<string> DiscoverRootAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        EnsureRepositoryPath(path);

        return Task.FromResult(
            Path.GetFullPath(
                repository.RepositoryPath));
    }

    public async Task<RepositoryInfo>
        GetRepositoryInfoAsync(
            string path,
            CancellationToken cancellationToken = default)
    {
        string root =
            await DiscoverRootAsync(
                    path,
                    cancellationToken)
                .ConfigureAwait(false);

        return new RepositoryInfo
        {
            RootPath = root,
            HeadCommit =
                repository.Head.ToLowerInvariant(),
            Branch = null,
            IsHeadDetached = true
        };
    }

    public async Task<string> ResolveCommitAsync(
        string path,
        string reference,
        CancellationToken cancellationToken = default)
    {
        _ = await DiscoverRootAsync(
                path,
                cancellationToken)
            .ConfigureAwait(false);

        if (!string.Equals(
                reference,
                "HEAD",
                StringComparison.Ordinal) &&
            !string.Equals(
                reference,
                repository.Head,
                StringComparison.OrdinalIgnoreCase))
        {
            throw Unsupported(
                "Resolving arbitrary Git references is not allowed.");
        }

        return repository.Head
            .ToLowerInvariant();
    }

    public Task<WorkingTreeStatus>
        GetWorkingTreeStatusAsync(
            string path,
            CancellationToken cancellationToken = default)
    {
        EnsureRepositoryPath(path);
        cancellationToken.ThrowIfCancellationRequested();

        throw Unsupported(
            "Working tree status is not required by secure context inventory.");
    }

    public async Task<IReadOnlyList<GitIndexEntry>>
        GetIndexEntriesAsync(
            string path,
            CancellationToken cancellationToken = default)
    {
        EnsureRepositoryPath(path);

        SecureToolResult result =
            await ExecuteAsync(
                    SecureToolOperation.GitListIndex,
                    cancellationToken)
                .ConfigureAwait(false);

        return GitStructuredOutputParser
            .ParseIndexEntries(
                result.StandardOutput);
    }

    public async Task<IReadOnlyList<string>>
        GetUntrackedFilesAsync(
            string path,
            CancellationToken cancellationToken = default)
    {
        EnsureRepositoryPath(path);

        SecureToolResult result =
            await ExecuteAsync(
                    SecureToolOperation.GitListUntracked,
                    cancellationToken)
                .ConfigureAwait(false);

        return GitStructuredOutputParser
            .ParsePathList(
                result.StandardOutput);
    }

    public Task<IReadOnlyList<GitChange>>
        GetChangesAsync(
            string path,
            string fromReference,
            string toReference,
            CancellationToken cancellationToken = default)
    {
        EnsureRepositoryPath(path);
        cancellationToken.ThrowIfCancellationRequested();

        throw Unsupported(
            "Arbitrary Git diff references are not allowed.");
    }

    public Task<bool> IsAncestorAsync(
        string path,
        string possibleAncestorReference,
        string descendantReference,
        CancellationToken cancellationToken = default)
    {
        EnsureRepositoryPath(path);
        cancellationToken.ThrowIfCancellationRequested();

        throw Unsupported(
            "Arbitrary Git ancestry queries are not allowed.");
    }

    private async Task<SecureToolResult>
        ExecuteAsync(
            SecureToolOperation operation,
            CancellationToken cancellationToken)
    {
        SecureToolResult result =
            await executor.ExecuteAsync(
                    new SecureToolRequest
                    {
                        Repository = repository,
                        Operation = operation,
                        CorrelationId =
                            correlationId,
                        Timeout =
                            TimeSpan.FromSeconds(30),
                        StandardOutputLimitBytes =
                            1_048_576,
                        StandardErrorLimitBytes =
                            262_144
                    },
                    cancellationToken)
                .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            return result;
        }

        GitErrorKind kind =
            result.Outcome switch
            {
                ToolExecutionOutcome.TimedOut =>
                    GitErrorKind.TimedOut,

                ToolExecutionOutcome
                    .OutputLimitExceeded =>
                    GitErrorKind.OutputLimitExceeded,

                ToolExecutionOutcome.Cancelled =>
                    GitErrorKind.CommandFailed,

                _ =>
                    GitErrorKind.CommandFailed
            };

        throw new GitClientException(
            kind,
            $"Secure Git operation failed: {result.ErrorCode}");
    }

    private void EnsureRepositoryPath(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            path.IndexOfAny(
                ['\0', '\r', '\n']) >= 0)
        {
            throw new GitClientException(
                GitErrorKind.PathNotFound,
                "Repository path is invalid.");
        }

        string requested;
        string expected;

        try
        {
            requested =
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(path));

            expected =
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(
                        repository.RepositoryPath));
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                  NotSupportedException or
                  PathTooLongException)
        {
            throw new GitClientException(
                GitErrorKind.PathNotFound,
                "Repository path is invalid.");
        }

        if (!string.Equals(
                requested,
                expected,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal))
        {
            throw new GitClientException(
                GitErrorKind.PathNotFound,
                "Repository path is outside the bound worktree.");
        }
    }

    private static GitClientException Unsupported(
        string message) =>
        new(
            GitErrorKind.CommandFailed,
            message);
}
