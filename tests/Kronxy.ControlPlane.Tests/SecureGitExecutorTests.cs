using System.Diagnostics;
using Kronxy.Infrastructure.Git;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class SecureGitExecutorTests
{
    [Fact]
    public async Task Resolve_commit_executes_successfully()
    {
        using var fixture = new Fixture();

        var result =
            await fixture.Executor.ExecuteAsync(
                fixture.Request(
                    SecureGitOperation.ResolveCommit)
                with
                {
                    Reference = "HEAD"
                });

        Assert.True(result.IsSuccess);
        Assert.Equal(
            SecureGitOutcome.Completed,
            result.Outcome);

        Assert.Equal(
            fixture.Head,
            result.StandardOutput.Trim());
    }

    [Fact]
    public async Task Show_top_level_returns_repository_root()
    {
        using var fixture = new Fixture();

        var result =
            await fixture.Executor.ExecuteAsync(
                fixture.Request(
                    SecureGitOperation.ShowTopLevel));

        Assert.True(result.IsSuccess);

        Assert.Equal(
            Path.GetFullPath(fixture.Repository),
            Path.GetFullPath(
                result.StandardOutput.Trim()));
    }

    [Fact]
    public async Task Current_branch_returns_exact_branch()
    {
        using var fixture = new Fixture();

        var result =
            await fixture.Executor.ExecuteAsync(
                fixture.Request(
                    SecureGitOperation.CurrentBranch));

        Assert.True(result.IsSuccess);

        Assert.Equal(
            fixture.Branch,
            result.StandardOutput.Trim());
    }

    [Fact]
    public async Task Status_reports_dirty_file()
    {
        using var fixture = new Fixture();

        await File.WriteAllTextAsync(
            Path.Combine(
                fixture.Repository,
                "tracked.txt"),
            "changed");

        var result =
            await fixture.Executor.ExecuteAsync(
                fixture.Request(
                    SecureGitOperation.Status));

        Assert.True(result.IsSuccess);

        Assert.Contains(
            "tracked.txt",
            result.StandardOutput,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Branch_exists_returns_zero_for_existing_branch()
    {
        using var fixture = new Fixture();

        var result =
            await fixture.Executor.ExecuteAsync(
                fixture.Request(
                    SecureGitOperation.BranchExists)
                with
                {
                    Branch = fixture.Branch
                });

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task Missing_branch_returns_nonzero()
    {
        using var fixture = new Fixture();

        var result =
            await fixture.Executor.ExecuteAsync(
                fixture.Request(
                    SecureGitOperation.BranchExists)
                with
                {
                    Branch =
                        "kronxy/jobs/krx-999999-missing"
                });

        Assert.False(result.IsSuccess);

        Assert.Equal(
            SecureGitOutcome.NonZeroExitCode,
            result.Outcome);
    }

    [Fact]
    public async Task Invalid_request_is_rejected_before_process()
    {
        using var fixture = new Fixture();

        var result =
            await fixture.Executor.ExecuteAsync(
                fixture.Request(
                    SecureGitOperation.ResolveCommit)
                with
                {
                    Reference = "-danger"
                });

        Assert.Equal(
            SecureGitOutcome.Rejected,
            result.Outcome);

        Assert.Null(result.ExitCode);
    }

    [Fact]
    public async Task Precancelled_request_is_cancelled()
    {
        using var fixture = new Fixture();

        using var source =
            new CancellationTokenSource();

        source.Cancel();

        var result =
            await fixture.Executor.ExecuteAsync(
                fixture.Request(
                    SecureGitOperation.Status),
                source.Token);

        Assert.Equal(
            SecureGitOutcome.Cancelled,
            result.Outcome);
    }

    [Fact]
    public async Task Invalid_output_limit_is_rejected()
    {
        using var fixture = new Fixture();

        var result =
            await fixture.Executor.ExecuteAsync(
                fixture.Request(
                    SecureGitOperation.Status)
                with
                {
                    StandardOutputLimitBytes =
                        20 * 1024 * 1024
                });

        Assert.Equal(
            SecureGitOutcome.Rejected,
            result.Outcome);
    }

    [Fact]
    public void Start_info_has_no_shell_and_controlled_environment()
    {
        using var fixture = new Fixture();

        var request =
            fixture.Request(
                SecureGitOperation.Status);

        var invocation =
            SecureGitInvocationBuilder.Build(
                request);

        Assert.NotNull(invocation);

        ProcessStartInfo startInfo =
            fixture.Executor.CreateStartInfo(
                invocation!);

        Assert.False(
            startInfo.UseShellExecute);

        Assert.Empty(
            startInfo.Arguments);

        Assert.Equal(
            fixture.GitExecutable,
            startInfo.FileName);

        string[] expectedKeys =
        [
            "GCM_INTERACTIVE",
            "GIT_CONFIG_GLOBAL",
            "GIT_CONFIG_NOSYSTEM",
            "GIT_OPTIONAL_LOCKS",
            "GIT_PAGER",
            "GIT_TERMINAL_PROMPT",
            "LANG",
            "LC_ALL",
            "PATH"
        ];

        Assert.Equal(
            expectedKeys,
            startInfo.Environment.Keys
                .OrderBy(
                    value => value,
                    StringComparer.Ordinal)
                .ToArray());

        Assert.DoesNotContain(
            startInfo.ArgumentList
                .Cast<string>(),
            value =>
                value is
                    "sh" or
                    "bash" or
                    "cmd" or
                    "powershell");
    }

    private sealed class Fixture :
        IDisposable
    {
        public Fixture()
        {
            Root =
                Path.Combine(
                    Path.GetTempPath(),
                    "kronxy-secure-git-executor",
                    Guid.NewGuid().ToString("N"));

            Repository =
                Path.Combine(
                    Root,
                    "repository");

            Directory.CreateDirectory(
                Repository);

            GitExecutable =
                ResolveGit();

            Git(
                "init");

            Git(
                "config",
                "user.name",
                "KRONXY Tests");

            Git(
                "config",
                "user.email",
                "kronxy@example.invalid");

            File.WriteAllText(
                Path.Combine(
                    Repository,
                    "tracked.txt"),
                "initial");

            Git(
                "add",
                "--all",
                "--");

            Git(
                "commit",
                "-m",
                "initial");

            Branch =
                Git(
                    "symbolic-ref",
                    "--quiet",
                    "--short",
                    "HEAD")
                .Trim();

            Head =
                Git(
                    "rev-parse",
                    "HEAD")
                .Trim();

            Executor =
                new SecureGitExecutor(
                    GitExecutable);
        }

        public string Root { get; }

        public string Repository { get; }

        public string GitExecutable { get; }

        public string Branch { get; }

        public string Head { get; }

        public SecureGitExecutor Executor { get; }

        public SecureGitRequest Request(
            SecureGitOperation operation)
        {
            return new SecureGitRequest
            {
                Operation = operation,
                WorkingDirectory = Repository,
                Timeout = TimeSpan.FromSeconds(15),
                StandardOutputLimitBytes = 4096,
                StandardErrorLimitBytes = 4096
            };
        }

        private string Git(
            params string[] arguments)
        {
            ProcessStartInfo info =
                new()
                {
                    FileName =
                        GitExecutable,

                    WorkingDirectory =
                        Repository,

                    UseShellExecute =
                        false,

                    RedirectStandardOutput =
                        true,

                    RedirectStandardError =
                        true
                };

            foreach (string argument
                     in arguments)
            {
                info.ArgumentList.Add(
                    argument);
            }

            using Process process =
                Process.Start(info)
                ?? throw new InvalidOperationException(
                    "Unable to start git.");

            string stdout =
                process.StandardOutput
                    .ReadToEnd();

            string stderr =
                process.StandardError
                    .ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    stderr);
            }

            return stdout;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(
                        Root,
                        recursive: true);
                }
            }
            catch
            {
            }
        }

        private static string ResolveGit()
        {
            string path =
                Environment.GetEnvironmentVariable(
                    "PATH")
                ?? string.Empty;

            foreach (string directory
                     in path.Split(
                         Path.PathSeparator,
                         StringSplitOptions
                             .RemoveEmptyEntries))
            {
                string candidate =
                    Path.Combine(
                        directory,
                        OperatingSystem.IsWindows()
                            ? "git.exe"
                            : "git");

                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(
                        candidate);
                }
            }

            throw new FileNotFoundException(
                "Git executable not found.");
        }
    }
}
