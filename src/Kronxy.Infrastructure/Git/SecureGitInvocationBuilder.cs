namespace Kronxy.Infrastructure.Git;

internal sealed record SecureGitInvocation(
    string WorkingDirectory,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string>? Environment = null);

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

            SecureGitOperation.StagePaths => BuildPaths(request, "add", "--all", "--"),
            SecureGitOperation.WriteTree => NoParameters(request, "write-tree"),
            SecureGitOperation.CommitTree => BuildCommitTree(request),
            SecureGitOperation.UpdateRef => BuildUpdateRef(request),
            SecureGitOperation.ResolveTree => BuildObjectQuery(request, "^{tree}"),
            SecureGitOperation.ResolveParent => BuildObjectQuery(request, "^"),
            SecureGitOperation.ReadTree => BuildCommitOnly(request, "read-tree"),
            SecureGitOperation.CheckoutIndexPaths =>
                BuildPaths(request, "checkout-index", "--force", "--"),
            SecureGitOperation.ChangedPaths => BuildChangedPaths(request),

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

    private static SecureGitInvocation? BuildPaths(
        SecureGitRequest request, params string[] prefix)
    {
        if (!Only(request, paths: true) || request.Paths is not { Count: > 0 and <= 100 } ||
            request.Paths.Any(path => !IsSafeRelativePath(path))) return null;
        return Invocation(request, [.. prefix, .. request.Paths]);
    }

    private static SecureGitInvocation? BuildCommitOnly(
        SecureGitRequest request, string command)
    {
        if (!Only(request, commit: true) || !IsObjectId(request.Commit)) return null;
        return Invocation(request, command, request.Commit!);
    }

    private static SecureGitInvocation? BuildObjectQuery(
        SecureGitRequest request, string suffix)
    {
        if (!Only(request, commit: true) || !IsObjectId(request.Commit)) return null;
        return Invocation(request, "rev-parse", "--verify", "--end-of-options",
            request.Commit! + suffix);
    }

    private static SecureGitInvocation? BuildChangedPaths(SecureGitRequest request)
    {
        if (!Only(request, commit: true) || !IsObjectId(request.Commit)) return null;
        return Invocation(request, "diff-tree", "--no-commit-id", "--name-only", "-r",
            request.Commit!, "--");
    }

    private static SecureGitInvocation? BuildUpdateRef(SecureGitRequest request)
    {
        if (!Only(request, reference: true, commit: true, oldCommit: true) ||
            !IsSafeHeadReference(request.Reference) || !IsObjectId(request.Commit) ||
            !IsObjectId(request.OldCommit)) return null;
        return Invocation(request, "update-ref", request.Reference!, request.Commit!,
            request.OldCommit!);
    }

    private static SecureGitInvocation? BuildCommitTree(SecureGitRequest request)
    {
        if (!Only(request, commit: true, oldCommit: true, message: true, actor: true, recordedAtUtc: true) ||
            !IsObjectId(request.Commit) || string.IsNullOrWhiteSpace(request.Message) ||
            request.Message.Length > 500 || !SafeText(request.Message) ||
            string.IsNullOrWhiteSpace(request.Actor) || request.Actor.Length > 200 ||
            !SafeText(request.Actor) || request.RecordedAtUtc is null) return null;
        string timestamp = request.RecordedAtUtc.Value.ToUniversalTime().ToString("O");
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GIT_AUTHOR_NAME"] = request.Actor!,
            ["GIT_AUTHOR_EMAIL"] = "operator@kronxy.invalid",
            ["GIT_AUTHOR_DATE"] = timestamp,
            ["GIT_COMMITTER_NAME"] = "KRONXY",
            ["GIT_COMMITTER_EMAIL"] = "control-plane@kronxy.invalid",
            ["GIT_COMMITTER_DATE"] = timestamp
        };
        return new SecureGitInvocation(Path.GetFullPath(request.WorkingDirectory),
            Array.AsReadOnly(new[] { "commit-tree", request.Commit!, "-p", request.OldCommit!, "-m", request.Message! }),
            environment);
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
        bool commit = false,
        bool oldCommit = false,
        bool paths = false,
        bool message = false,
        bool actor = false,
        bool recordedAtUtc = false)
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
                : request.Commit is null) &&
            (oldCommit ? request.OldCommit is not null : request.OldCommit is null) &&
            (paths ? request.Paths is not null : request.Paths is null) &&
            (message ? request.Message is not null : request.Message is null) &&
            (actor ? request.Actor is not null : request.Actor is null) &&
            (recordedAtUtc ? request.RecordedAtUtc is not null : request.RecordedAtUtc is null);
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

    private static bool IsSafeHeadReference(string? value) =>
        value is not null && value.StartsWith("refs/heads/", StringComparison.Ordinal) &&
        IsSafeBranch(value[11..]);

    private static bool IsSafeRelativePath(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 512 &&
        value[0] != '-' && !Path.IsPathFullyQualified(value) &&
        value.IndexOfAny(['\0', '\r', '\n', '\\']) < 0 &&
        value.Split('/').All(segment => segment.Length > 0 && segment is not "." and not "..");

    private static bool SafeText(string value) =>
        value.IndexOfAny(['\0', '\r', '\n']) < 0;

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
