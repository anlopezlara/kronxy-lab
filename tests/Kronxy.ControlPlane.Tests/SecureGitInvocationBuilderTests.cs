using Kronxy.Infrastructure.Git;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class SecureGitInvocationBuilderTests
{
    private static readonly string Root =
        Path.GetFullPath(
            Path.Combine(
                Path.GetTempPath(),
                "kronxy-secure-git-tests"));

    private static readonly string Worktree =
        Path.Combine(
            Root,
            "worktrees",
            "KRX-000001");

    private const string Branch =
        "kronxy/jobs/krx-000001-abcdef123456";

    private const string Commit =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void Resolve_commit_is_exact()
    {
        var invocation =
            Build(
                SecureGitOperation.ResolveCommit,
                reference: "HEAD");

        Assert.Equal(
            Root,
            invocation.WorkingDirectory);

        Assert.Equal(
            [
                "rev-parse",
                "--verify",
                "--end-of-options",
                "HEAD^{commit}"
            ],
            invocation.Arguments);
    }

    [Fact]
    public void Show_top_level_is_exact()
    {
        AssertArguments(
            SecureGitOperation.ShowTopLevel,
            "rev-parse",
            "--show-toplevel");
    }

    [Fact]
    public void Common_directory_is_exact()
    {
        AssertArguments(
            SecureGitOperation.CommonDirectory,
            "rev-parse",
            "--git-common-dir");
    }

    [Fact]
    public void Current_branch_is_exact()
    {
        AssertArguments(
            SecureGitOperation.CurrentBranch,
            "symbolic-ref",
            "--quiet",
            "--short",
            "HEAD");
    }

    [Fact]
    public void Branch_exists_is_exact()
    {
        var invocation =
            Build(
                SecureGitOperation.BranchExists,
                branch: Branch);

        Assert.Equal(
            [
                "show-ref",
                "--verify",
                "--quiet",
                $"refs/heads/{Branch}"
            ],
            invocation.Arguments);
    }

    [Fact]
    public void Worktree_add_existing_branch_is_exact()
    {
        var invocation =
            Build(
                SecureGitOperation
                    .WorktreeAddExistingBranch,
                branch: Branch,
                worktreePath: Worktree);

        Assert.Equal(
            [
                "worktree",
                "add",
                Worktree,
                Branch
            ],
            invocation.Arguments);
    }

    [Fact]
    public void Worktree_add_new_branch_is_exact()
    {
        var invocation =
            Build(
                SecureGitOperation
                    .WorktreeAddNewBranch,
                branch: Branch,
                worktreePath: Worktree,
                commit: Commit);

        Assert.Equal(
            [
                "worktree",
                "add",
                "-b",
                Branch,
                Worktree,
                Commit
            ],
            invocation.Arguments);
    }

    [Fact]
    public void Worktree_prune_is_exact()
    {
        AssertArguments(
            SecureGitOperation.WorktreePrune,
            "worktree",
            "prune");
    }

    [Fact]
    public void Worktree_remove_is_exact()
    {
        var invocation =
            Build(
                SecureGitOperation.WorktreeRemove,
                worktreePath: Worktree);

        Assert.Equal(
            [
                "worktree",
                "remove",
                Worktree
            ],
            invocation.Arguments);
    }

    [Fact]
    public void Status_is_exact()
    {
        AssertArguments(
            SecureGitOperation.Status,
            "status",
            "--porcelain=v1",
            "--untracked-files=all");
    }

    [Fact]
    public void Diff_is_exact()
    {
        AssertArguments(
            SecureGitOperation.Diff,
            "diff",
            "--no-ext-diff",
            "--no-textconv",
            "HEAD",
            "--");
    }

    [Fact]
    public void Diff_stat_is_exact()
    {
        AssertArguments(
            SecureGitOperation.DiffStat,
            "diff",
            "--stat",
            "--no-ext-diff",
            "--no-textconv",
            "HEAD",
            "--");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("-HEAD")]
    [InlineData("HEAD\nstatus")]
    [InlineData("HEAD\rstatus")]
    [InlineData("HEAD\0status")]
    public void Unsafe_reference_is_rejected(
        string reference)
    {
        Assert.Null(
            SecureGitInvocationBuilder.Build(
                Request(
                    SecureGitOperation.ResolveCommit)
                with
                {
                    Reference = reference
                }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("-danger")]
    [InlineData("branch name")]
    [InlineData("../escape")]
    [InlineData("a..b")]
    [InlineData("a@{b")]
    [InlineData("a.lock")]
    [InlineData("/absolute")]
    [InlineData("a//b")]
    [InlineData("a:b")]
    [InlineData("a?b")]
    [InlineData("a*b")]
    [InlineData("a[b")]
    [InlineData("a\\b")]
    [InlineData("a\nb")]
    public void Unsafe_branch_is_rejected(
        string branch)
    {
        Assert.Null(
            SecureGitInvocationBuilder.Build(
                Request(
                    SecureGitOperation.BranchExists)
                with
                {
                    Branch = branch
                }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData(
        "gggggggggggggggggggggggggggggggggggggggg")]
    [InlineData(
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Invalid_commit_is_rejected(
        string commit)
    {
        Assert.Null(
            SecureGitInvocationBuilder.Build(
                Request(
                    SecureGitOperation
                        .WorktreeAddNewBranch)
                with
                {
                    Branch = Branch,
                    WorktreePath = Worktree,
                    Commit = commit
                }));
    }

    [Fact]
    public void Relative_worktree_path_is_rejected()
    {
        Assert.Null(
            SecureGitInvocationBuilder.Build(
                Request(
                    SecureGitOperation.WorktreeRemove)
                with
                {
                    WorktreePath =
                        "../repository"
                }));
    }

    [Fact]
    public void Working_directory_as_worktree_is_rejected()
    {
        Assert.Null(
            SecureGitInvocationBuilder.Build(
                Request(
                    SecureGitOperation.WorktreeRemove)
                with
                {
                    WorktreePath = Root
                }));
    }

    [Fact]
    public void Extra_parameter_is_rejected()
    {
        Assert.Null(
            SecureGitInvocationBuilder.Build(
                Request(
                    SecureGitOperation.Status)
                with
                {
                    Reference = "HEAD"
                }));
    }

    [Fact]
    public void Unknown_operation_is_rejected()
    {
        Assert.Null(
            SecureGitInvocationBuilder.Build(
                Request(
                    (SecureGitOperation)999)));
    }

    [Fact]
    public void Invalid_timeout_is_rejected()
    {
        Assert.Null(
            SecureGitInvocationBuilder.Build(
                Request(
                    SecureGitOperation.Status)
                with
                {
                    Timeout = TimeSpan.Zero
                }));
    }

    [Fact]
    public void Invalid_output_limit_is_rejected()
    {
        Assert.Null(
            SecureGitInvocationBuilder.Build(
                Request(
                    SecureGitOperation.Status)
                with
                {
                    StandardOutputLimitBytes = 0
                }));
    }

    private static SecureGitInvocation Build(
        SecureGitOperation operation,
        string? reference = null,
        string? branch = null,
        string? worktreePath = null,
        string? commit = null)
    {
        var result =
            SecureGitInvocationBuilder.Build(
                Request(operation)
                with
                {
                    Reference = reference,
                    Branch = branch,
                    WorktreePath = worktreePath,
                    Commit = commit
                });

        return Assert.IsType<SecureGitInvocation>(
            result);
    }

    private static SecureGitRequest Request(
        SecureGitOperation operation)
    {
        return new SecureGitRequest
        {
            Operation = operation,
            WorkingDirectory = Root,
            Timeout = TimeSpan.FromSeconds(15),
            StandardOutputLimitBytes = 4096,
            StandardErrorLimitBytes = 4096
        };
    }

    private static void AssertArguments(
        SecureGitOperation operation,
        params string[] expected)
    {
        var invocation =
            Build(operation);

        Assert.Equal(
            expected,
            invocation.Arguments);
    }
}
