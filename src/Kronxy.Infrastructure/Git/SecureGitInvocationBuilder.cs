namespace Kronxy.Infrastructure.Git;

internal sealed record SecureGitInvocation(
    string WorkingDirectory,
    IReadOnlyList<string> Arguments);

internal static class SecureGitInvocationBuilder
{
    public static SecureGitInvocation? Build(
        SecureGitRequest request)
    {
        if (!TryValidateCommon(request))
        {
            return null;
        }

        return request.Operation switch
        {
            SecureGitOperation.ResolveCommit =>
                BuildResolveCommit(request),

            SecureGitOperation.ShowTopLevel =>
                NoParameters(
                    request,
                    "rev-parse",
                    "--show-toplevel"),

            SecureGitOperation.CommonDirectory =>
                NoParameters(
                    request,
                    "rev-parse",
                    "--git-common-dir"),

            SecureGitOperation.CurrentBranch =>
                NoParameters(
                    request,
                    "symbolic-ref",
                    "--quiet",
                    "--short",
                    "HEAD"),

            SecureGitOperation.BranchExists =>
                BuildBranchExists(request),

            SecureGitOperation.WorktreeAddExistingBranch =>
                BuildWorktreeAddExistingBranch(request),

            SecureGitOperation.WorktreeAddNewBranch =>
                BuildWorktreeAddNewBranch(request),

            SecureGitOperation.WorktreePrune =>
                NoParameters(
                    request,
                    "worktree",
                    "prune"),

            SecureGitOperation.WorktreeRemove =>
                BuildWorktreeRemove(request),

            SecureGitOperation.Status =>
                NoParameters(
                    request,
                    "status",
                    "--porcelain=v1",
                    "--untracked-files=all"),

            SecureGitOperation.StatusNull =>
                NoParameters(
                    request,
                    "status",
                    "--porcelain=v1",
                    "-z",
                    "--untracked-files=all"),

            SecureGitOperation.Diff =>
                NoParameters(
                    request,
                    "diff",
                    "--no-ext-diff",
                    "--no-textconv",
                    "HEAD",
                    "--"),

            SecureGitOperation.DiffStat =>
                NoParameters(
                    request,
                    "diff",
                    "--stat",
                    "--no-ext-diff",
                    "--no-textconv",
                    "HEAD",
                    "--"),

            _ => null
        };
    }

    private static SecureGitInvocation? BuildResolveCommit(
        SecureGitRequest request)
    {
        if (!Only(
                request,
                reference: true) ||
            !IsSafeReference(request.Reference))
        {
            return null;
        }

        return Invocation(
            request,
            "rev-parse",
            "--verify",
            "--end-of-options",
            request.Reference! + "^{commit}");
    }

    private static SecureGitInvocation? BuildBranchExists(
        SecureGitRequest request)
    {
        if (!Only(
                request,
                branch: true) ||
            !IsSafeBranch(request.Branch))
        {
            return null;
        }

        return Invocation(
            request,
            "show-ref",
            "--verify",
            "--quiet",
            $"refs/heads/{request.Branch}");
    }

    private static SecureGitInvocation?
        BuildWorktreeAddExistingBranch(
            SecureGitRequest request)
    {
        if (!Only(
                request,
                branch: true,
                worktreePath: true) ||
            !IsSafeBranch(request.Branch) ||
            !IsSafeWorktreePath(
                request.WorkingDirectory,
                request.WorktreePath))
        {
            return null;
        }

        return Invocation(
            request,
            "worktree",
            "add",
            request.WorktreePath!,
            request.Branch!);
    }

    private static SecureGitInvocation?
        BuildWorktreeAddNewBranch(
            SecureGitRequest request)
    {
        if (!Only(
                request,
                branch: true,
                worktreePath: true,
                commit: true) ||
            !IsSafeBranch(request.Branch) ||
            !IsSafeWorktreePath(
                request.WorkingDirectory,
                request.WorktreePath) ||
            !IsObjectId(request.Commit))
        {
            return null;
        }

        return Invocation(
            request,
            "worktree",
            "add",
            "-b",
            request.Branch!,
            request.WorktreePath!,
            request.Commit!);
    }

    private static SecureGitInvocation? BuildWorktreeRemove(
        SecureGitRequest request)
    {
        if (!Only(
                request,
                worktreePath: true) ||
            !IsSafeWorktreePath(
                request.WorkingDirectory,
                request.WorktreePath))
        {
            return null;
        }

        return Invocation(
            request,
            "worktree",
            "remove",
            request.WorktreePath!);
    }

    private static SecureGitInvocation? NoParameters(
        SecureGitRequest request,
        params string[] arguments)
    {
        if (!Only(request))
        {
            return null;
        }

        return Invocation(
            request,
            arguments);
    }

    private static SecureGitInvocation Invocation(
        SecureGitRequest request,
        params string[] arguments)
    {
        return new SecureGitInvocation(
            Path.GetFullPath(
                request.WorkingDirectory),
            Array.AsReadOnly(arguments));
    }

    private static bool TryValidateCommon(
        SecureGitRequest? request)
    {
        if (request is null ||
            !Enum.IsDefined(request.Operation) ||
            string.IsNullOrWhiteSpace(
                request.WorkingDirectory) ||
            request.WorkingDirectory.IndexOfAny(
                ['\0', '\r', '\n']) >= 0 ||
            !Path.IsPathFullyQualified(
                request.WorkingDirectory) ||
            request.Timeout <= TimeSpan.Zero ||
            request.StandardOutputLimitBytes <= 0 ||
            request.StandardErrorLimitBytes <= 0)
        {
            return false;
        }

        try
        {
            _ = Path.GetFullPath(
                request.WorkingDirectory);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool Only(
        SecureGitRequest request,
        bool reference = false,
        bool branch = false,
        bool worktreePath = false,
        bool commit = false)
    {
        return
            (reference
                ? request.Reference is not null
                : request.Reference is null) &&

            (branch
                ? request.Branch is not null
                : request.Branch is null) &&

            (worktreePath
                ? request.WorktreePath is not null
                : request.WorktreePath is null) &&

            (commit
                ? request.Commit is not null
                : request.Commit is null);
    }

    private static bool IsSafeReference(
        string? value)
    {
        return
            !string.IsNullOrWhiteSpace(value) &&
            value.Length <= 1024 &&
            value[0] != '-' &&
            value.IndexOfAny(
                ['\0', '\r', '\n']) < 0;
    }

    private static bool IsSafeBranch(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > 255 ||
            value[0] == '-' ||
            value.IndexOfAny(
                [
                    '\0',
                    '\r',
                    '\n',
                    ' ',
                    '~',
                    '^',
                    ':',
                    '?',
                    '*',
                    '[',
                    '\\'
                ]) >= 0 ||
            value.Contains("..", StringComparison.Ordinal) ||
            value.Contains("@{", StringComparison.Ordinal) ||
            value.EndsWith(
                ".lock",
                StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith(
                "/",
                StringComparison.Ordinal) ||
            value.StartsWith(
                "/",
                StringComparison.Ordinal) ||
            value.Contains(
                "//",
                StringComparison.Ordinal))
        {
            return false;
        }

        return value.All(
            character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is
                    '/' or '-' or '_' or '.');
    }

    private static bool IsSafeWorktreePath(
        string workingDirectory,
        string? worktreePath)
    {
        if (string.IsNullOrWhiteSpace(
                worktreePath) ||
            worktreePath.IndexOfAny(
                ['\0', '\r', '\n']) >= 0 ||
            !Path.IsPathFullyQualified(
                worktreePath))
        {
            return false;
        }

        try
        {
            string root =
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(
                        workingDirectory));

            string candidate =
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(
                        worktreePath));

            if (PathEquals(
                    root,
                    candidate))
            {
                return false;
            }

            return !ContainsTraversalSegments(
                worktreePath);
        }
        catch
        {
            return false;
        }
    }

    private static bool ContainsTraversalSegments(
        string path)
    {
        string[] parts =
            path.Split(
                [
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                ],
                StringSplitOptions
                    .RemoveEmptyEntries);

        return parts.Any(
            part =>
                part is "." or "..");
    }

    private static bool IsObjectId(
        string? value)
    {
        return
            value is not null &&
            value.Length is 40 or 64 &&
            value.All(Uri.IsHexDigit);
    }

    private static bool PathEquals(
        string left,
        string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
    }
}
