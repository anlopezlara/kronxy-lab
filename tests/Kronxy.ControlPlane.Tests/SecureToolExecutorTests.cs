using System.Diagnostics;
using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;
using Kronxy.Application.Workspaces;
using Kronxy.Infrastructure.Execution;
using Kronxy.Infrastructure.Git;
using Kronxy.Infrastructure.Workspaces;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class SecureToolExecutorTests
{
    [Fact]
    public async Task Git_status_executes_from_registered_allowlist()
    {
        using var fixture = new Fixture();

        var request =
            fixture.Request(
                SecureToolOperation.GitStatus);

        var result =
            await fixture.Executor
                .ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ToolExecutionOutcome.Completed,
            result.Outcome);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task Git_diff_reports_workspace_change()
    {
        using var fixture = new Fixture();

        await File.WriteAllTextAsync(
            Path.Combine(
                fixture.Repository.RepositoryPath,
                "tracked.txt"),
            "changed");

        var result =
            await fixture.Executor.ExecuteAsync(
                fixture.Request(
                    SecureToolOperation.GitDiff));

        Assert.True(result.IsSuccess);

        Assert.Contains(
            "tracked.txt",
            result.StandardOutput,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../escape.csproj")]
    [InlineData("../../escape.csproj")]
    [InlineData("/tmp/escape.csproj")]
    [InlineData("x;touch.csproj")]
    [InlineData("$(whoami).csproj")]
    [InlineData("`whoami`.csproj")]
    [InlineData("x|cat.csproj")]
    [InlineData("x\nbad.csproj")]
    public async Task Adversarial_dotnet_targets_are_rejected(
        string target)
    {
        using var fixture = new Fixture();

        var result =
            await fixture.Executor.ExecuteAsync(
                fixture.Request(
                    SecureToolOperation.DotnetBuild,
                    target));

        Assert.Equal(
            ToolExecutionOutcome.Rejected,
            result.Outcome);

        Assert.Null(result.ExitCode);
    }

    [Fact]
    public async Task Absolute_target_is_rejected()
    {
        using var fixture = new Fixture();

        var result =
            await fixture.Executor.ExecuteAsync(
                fixture.Request(
                    SecureToolOperation.DotnetBuild,
                    Path.Combine(
                        Path.GetTempPath(),
                        "sample.csproj")));

        Assert.Equal(
            ToolExecutionOutcome.Rejected,
            result.Outcome);
    }

    [Fact]
    public async Task Git_operation_rejects_user_target()
    {
        using var fixture = new Fixture();

        var result =
            await fixture.Executor.ExecuteAsync(
                fixture.Request(
                    SecureToolOperation.GitStatus,
                    "anything"));

        Assert.Equal(
            ToolExecutionOutcome.Rejected,
            result.Outcome);

        Assert.Equal(
            "TOOL_TARGET_NOT_ALLOWED",
            result.ErrorCode);
    }

    [Fact]
    public async Task Unknown_operation_is_rejected()
    {
        using var fixture = new Fixture();

        var request =
            fixture.Request(
                (SecureToolOperation)999);

        var result =
            await fixture.Executor
                .ExecuteAsync(request);

        Assert.Equal(
            ToolExecutionOutcome.Rejected,
            result.Outcome);

        Assert.Equal(
            "TOOL_OPERATION_NOT_ALLOWED",
            result.ErrorCode);
    }

    [Fact]
    public async Task Precancelled_request_returns_cancelled()
    {
        using var fixture = new Fixture();

        using var source =
            new CancellationTokenSource();

        source.Cancel();

        var result =
            await fixture.Executor.ExecuteAsync(
                fixture.Request(
                    SecureToolOperation.GitStatus),
                source.Token);

        Assert.Equal(
            ToolExecutionOutcome.Cancelled,
            result.Outcome);
    }

    [Fact]
    public async Task Output_limit_terminates_large_git_diff()
    {
        using var fixture = new Fixture();

        await File.WriteAllTextAsync(
            Path.Combine(
                fixture.Repository.RepositoryPath,
                "tracked.txt"),
            new string('x', 200_000));

        var request =
            fixture.Request(
                SecureToolOperation.GitDiff)
            with
            {
                StandardOutputLimitBytes = 128
            };

        var result =
            await fixture.Executor
                .ExecuteAsync(request);

        Assert.Equal(
            ToolExecutionOutcome.OutputLimitExceeded,
            result.Outcome);

        Assert.True(
            System.Text.Encoding.UTF8
                .GetByteCount(
                    result.StandardOutput) <= 128);
    }

    [Fact]
    public async Task Dotnet_restore_accepts_only_existing_relative_project()
    {
        using var fixture = new Fixture();

        var result =
            await fixture.Executor.ExecuteAsync(
                fixture.Request(
                    SecureToolOperation.DotnetRestore,
                    "Sample.csproj")
                with
                {
                    Timeout =
                        TimeSpan.FromSeconds(30)
                });

        Assert.True(
            result.Outcome is
                ToolExecutionOutcome.Completed or
                ToolExecutionOutcome.NonZeroExitCode);

        Assert.NotEqual(
            ToolExecutionOutcome.Rejected,
            result.Outcome);
    }

    [Fact]
    public async Task Very_short_timeout_terminates_dotnet_process()
    {
        using var fixture = new Fixture();

        var request =
            fixture.Request(
                SecureToolOperation.DotnetRestore,
                "Sample.csproj")
            with
            {
                Timeout =
                    TimeSpan.FromMilliseconds(1)
            };

        var result =
            await fixture.Executor
                .ExecuteAsync(request);

        Assert.Equal(
            ToolExecutionOutcome.TimedOut,
            result.Outcome);
    }

    [Fact]
    public async Task Forged_repository_outside_workspace_is_rejected()
    {
        using var fixture = new Fixture();

        var forged =
            fixture.Repository with
            {
                RepositoryPath =
                    fixture.AuthoritativeRepository
            };

        var request =
            fixture.Request(
                SecureToolOperation.GitStatus)
            with
            {
                Repository = forged
            };

        var result =
            await fixture.Executor
                .ExecuteAsync(request);

        Assert.Equal(
            ToolExecutionOutcome.Rejected,
            result.Outcome);

        Assert.Equal(
            "TOOL_REPOSITORY_OUTSIDE_WORKSPACE",
            result.ErrorCode);
    }

    [Fact]
    public async Task Dotnet_test_rejects_symlink_results_directory()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture =
            new Fixture();

        string kronxyDirectory =
            Path.Combine(
                fixture.Repository.WorkspacePath,
                ".kronxy");

        Directory.CreateDirectory(
            kronxyDirectory);

        string outside =
            Path.Combine(
                Path.GetDirectoryName(
                    fixture.Repository.WorkspacePath)!,
                "outside-test-results-" +
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            outside);

        string results =
            Path.Combine(
                kronxyDirectory,
                "test-results");

        Directory.CreateSymbolicLink(
            results,
            outside);

        try
        {
            SecureToolResult result =
                await fixture.Executor.ExecuteAsync(
                    fixture.Request(
                        SecureToolOperation.DotnetTest,
                        "Sample.csproj"));

            Assert.Equal(
                ToolExecutionOutcome.Rejected,
                result.Outcome);

            Assert.Equal(
                "TOOL_TEST_RESULTS_PATH_UNSAFE",
                result.ErrorCode);
        }
        finally
        {
            if (Directory.Exists(results))
            {
                Directory.Delete(results);
            }

            if (Directory.Exists(outside))
            {
                Directory.Delete(
                    outside,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task Dotnet_test_rejects_symlink_kronxy_directory()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture =
            new Fixture();

        string outside =
            Path.Combine(
                Path.GetDirectoryName(
                    fixture.Repository.WorkspacePath)!,
                "outside-kronxy-" +
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            outside);

        string kronxyDirectory =
            Path.Combine(
                fixture.Repository.WorkspacePath,
                ".kronxy");

        Directory.CreateSymbolicLink(
            kronxyDirectory,
            outside);

        try
        {
            SecureToolResult result =
                await fixture.Executor.ExecuteAsync(
                    fixture.Request(
                        SecureToolOperation.DotnetTest,
                        "Sample.csproj"));

            Assert.Equal(
                ToolExecutionOutcome.Rejected,
                result.Outcome);

            Assert.Equal(
                "TOOL_TEST_RESULTS_PATH_UNSAFE",
                result.ErrorCode);
        }
        finally
        {
            if (Directory.Exists(
                    kronxyDirectory))
            {
                Directory.Delete(
                    kronxyDirectory);
            }

            if (Directory.Exists(
                    outside))
            {
                Directory.Delete(
                    outside,
                    recursive: true);
            }
        }
    }

    [Fact]
    public void Invocation_is_structured_and_has_no_shell()
    {
        using var fixture = new Fixture();

        var request =
            fixture.Request(
                SecureToolOperation.GitStatus);

        var invocation =
            fixture.Executor
                .BuildInvocation(request);

        Assert.NotNull(invocation);

        var startInfo =
            fixture.Executor
                .CreateStartInfo(
                    request,
                    invocation!);

        Assert.False(
            startInfo.UseShellExecute);

        Assert.Empty(
            startInfo.Arguments);

        Assert.Equal(
            invocation!.Arguments,
            startInfo.ArgumentList
                .Cast<string>());

        Assert.DoesNotContain(
            startInfo.ArgumentList
                .Cast<string>(),
            argument =>
                argument is
                    "-c" or
                    "--command");
    }

    [Fact]
    public void Environment_is_rebuilt_from_controlled_allowlist()
    {
        using var fixture = new Fixture();

        var request =
            fixture.Request(
                SecureToolOperation.GitStatus);

        var invocation =
            fixture.Executor
                .BuildInvocation(request)!;

        var startInfo =
            fixture.Executor
                .CreateStartInfo(
                    request,
                    invocation);

        var keys =
            startInfo.Environment.Keys
                .OrderBy(
                    value => value,
                    StringComparer.Ordinal)
                .ToArray();

        var expected =
            new[]
            {
                "DOTNET_CLI_HOME",
                "DOTNET_CLI_TELEMETRY_OPTOUT",
                "DOTNET_NOLOGO",
                "DOTNET_SKIP_FIRST_TIME_EXPERIENCE",
                "GCM_INTERACTIVE",
                "GIT_CONFIG_GLOBAL",
                "GIT_CONFIG_NOSYSTEM",
                "GIT_OPTIONAL_LOCKS",
                "GIT_PAGER",
                "GIT_TERMINAL_PROMPT",
                "HOME",
                "LANG",
                "LC_ALL",
                "PATH"
            }
            .OrderBy(
                value => value,
                StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            expected,
            keys);
    }

    [Fact]
    public void Public_contract_has_no_generic_command_property()
    {
        var properties =
            typeof(SecureToolRequest)
                .GetProperties()
                .Select(property =>
                    property.Name)
                .ToArray();

        Assert.DoesNotContain(
            "Command",
            properties);

        Assert.DoesNotContain(
            "FileName",
            properties);

        Assert.DoesNotContain(
            "Arguments",
            properties);

        Assert.DoesNotContain(
            "ArgumentsLine",
            properties);
    }

    [Fact]
    public async Task Forged_job_id_is_rejected_by_workspace_marker()
    {
        using var fixture = new Fixture();

        var forged =
            fixture.Repository with
            {
                JobId = Guid.NewGuid()
            };

        var result =
            await fixture.Executor.ExecuteAsync(
                fixture.Request(
                    SecureToolOperation.GitStatus)
                with
                {
                    Repository = forged
                });

        Assert.Equal(
            ToolExecutionOutcome.Rejected,
            result.Outcome);

        Assert.Equal(
            "TOOL_WORKSPACE_OWNERSHIP_INVALID",
            result.ErrorCode);
    }

    [Fact]
    public async Task Forged_external_id_is_rejected_by_workspace_marker()
    {
        using var fixture = new Fixture();

        var forged =
            fixture.Repository with
            {
                JobExternalId = "KRX-999999"
            };

        var result =
            await fixture.Executor.ExecuteAsync(
                fixture.Request(
                    SecureToolOperation.GitStatus)
                with
                {
                    Repository = forged
                });

        Assert.Equal(
            ToolExecutionOutcome.Rejected,
            result.Outcome);

        Assert.Equal(
            "TOOL_WORKSPACE_OWNERSHIP_INVALID",
            result.ErrorCode);
    }

    [Fact]
    public async Task Tampered_workspace_marker_is_rejected()
    {
        using var fixture = new Fixture();

        var marker =
            Path.Combine(
                fixture.Repository.WorkspacePath,
                ".kronxy-workspace.json");

        await File.WriteAllTextAsync(
            marker,
            """
            {
              "Version": 1,
              "JobId": "00000000-0000-0000-0000-000000000001",
              "JobExternalId": "KRX-000301"
            }
            """);

        var result =
            await fixture.Executor.ExecuteAsync(
                fixture.Request(
                    SecureToolOperation.GitStatus));

        Assert.Equal(
            ToolExecutionOutcome.Rejected,
            result.Outcome);

        Assert.Equal(
            "TOOL_WORKSPACE_OWNERSHIP_INVALID",
            result.ErrorCode);
    }

    [Fact]
    public async Task Correlation_id_with_newline_is_rejected()
    {
        using var fixture = new Fixture();

        var request =
            fixture.Request(
                SecureToolOperation.GitStatus)
            with
            {
                CorrelationId =
                    "safe\nforged"
            };

        var result =
            await fixture.Executor
                .ExecuteAsync(request);

        Assert.Equal(
            ToolExecutionOutcome.Rejected,
            result.Outcome);

        Assert.Equal(
            "TOOL_INVALID_CORRELATION_ID",
            result.ErrorCode);
    }

    [Fact]
    public async Task Timeout_above_maximum_is_rejected_before_execution()
    {
        using var fixture = new Fixture();

        var request =
            fixture.Request(
                SecureToolOperation.GitStatus)
            with
            {
                Timeout =
                    TimeSpan.FromHours(1)
            };

        var result =
            await fixture.Executor
                .ExecuteAsync(request);

        Assert.Equal(
            ToolExecutionOutcome.Rejected,
            result.Outcome);

        Assert.Equal(
            "TOOL_INVALID_TIMEOUT",
            result.ErrorCode);
    }

    [Fact]
    public async Task Output_limit_above_maximum_is_rejected_before_execution()
    {
        using var fixture = new Fixture();

        var request =
            fixture.Request(
                SecureToolOperation.GitStatus)
            with
            {
                StandardOutputLimitBytes =
                    20 * 1024 * 1024
            };

        var result =
            await fixture.Executor
                .ExecuteAsync(request);

        Assert.Equal(
            ToolExecutionOutcome.Rejected,
            result.Outcome);

        Assert.Equal(
            "TOOL_INVALID_OUTPUT_LIMIT",
            result.ErrorCode);
    }

    [Fact]
    public void Relative_executable_configuration_is_rejected()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new SecureToolExecutor(
                    SecureToolExecutorOptions.Create(
                        "dotnet",
                        TestToolResolver.Git())));
    }

    [Fact]
    public void Missing_absolute_executable_is_rejected()
    {
        var missing =
            Path.Combine(
                Path.GetTempPath(),
                Guid.NewGuid().ToString("N"),
                "missing-tool");

        Assert.Throws<ArgumentException>(
            () =>
                new SecureToolExecutor(
                    SecureToolExecutorOptions.Create(
                        missing,
                        TestToolResolver.Git())));
    }

    private sealed class Fixture :
        IDisposable
    {
        private readonly string container;

        public Fixture()
        {
            container =
                Path.Combine(
                    Path.GetTempPath(),
                    "kronxy-secure-tool-tests",
                    Guid.NewGuid()
                        .ToString("N"));

            AuthoritativeRepository =
                Path.Combine(
                    container,
                    "authoritative");

            WorkspaceRoot =
                Path.Combine(
                    container,
                    "workspaces");

            Directory.CreateDirectory(
                AuthoritativeRepository);

            Directory.CreateDirectory(
                WorkspaceRoot);

            Git(
                AuthoritativeRepository,
                "init",
                "-b",
                "main");

            Git(
                AuthoritativeRepository,
                "config",
                "user.name",
                "KRONXY Tests");

            Git(
                AuthoritativeRepository,
                "config",
                "user.email",
                "kronxy@example.invalid");

            File.WriteAllText(
                Path.Combine(
                    AuthoritativeRepository,
                    "tracked.txt"),
                "initial");

            File.WriteAllText(
                Path.Combine(
                    AuthoritativeRepository,
                    "Sample.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            Git(
                AuthoritativeRepository,
                "add",
                "--all",
                "--");

            Git(
                AuthoritativeRepository,
                "commit",
                "-m",
                "initial");

            var workspaceManager =
                new WorkspaceManager(
                    WorkspaceRoot);

            var workspaceResult =
                workspaceManager
                    .PrepareAsync(
                        Guid.NewGuid(),
                        "KRX-000301")
                    .GetAwaiter()
                    .GetResult();

            if (!workspaceResult.IsSuccess ||
                workspaceResult.Workspace is null)
            {
                throw new InvalidOperationException(
                    "Unable to prepare temporary workspace.");
            }

            var repositoryManager =
                new RepositoryManager(
                    AuthoritativeRepository,
                    WorkspaceRoot,
                    TestToolResolver.Git());

            var head =
                Git(
                    AuthoritativeRepository,
                    "rev-parse",
                    "HEAD").Trim();

            var repositoryResult =
                repositoryManager
                    .PrepareWorktreeAsync(
                        workspaceResult.Workspace,
                        head)
                    .GetAwaiter()
                    .GetResult();

            if (!repositoryResult.IsSuccess ||
                repositoryResult.Value is null)
            {
                throw new InvalidOperationException(
                    "Unable to prepare temporary worktree.");
            }

            Repository =
                repositoryResult.Value;

            var dotnetExecutable =
                Environment.GetEnvironmentVariable(
                    "DOTNET_HOST_PATH");

            if (string.IsNullOrWhiteSpace(
                    dotnetExecutable))
            {
                dotnetExecutable =
                    TestToolResolver.Dotnet();
            }

            var gitExecutable =
                TestToolResolver.Git();

            Executor =
                new SecureToolExecutor(
                    SecureToolExecutorOptions.Create(
                        dotnetExecutable,
                        gitExecutable));
        }

        public string AuthoritativeRepository { get; }

        public string WorkspaceRoot { get; }

        public RepositoryWorktreeHandle Repository { get; }

        public SecureToolExecutor Executor { get; }

        public SecureToolRequest Request(
            SecureToolOperation operation,
            string? target = null) =>
            new()
            {
                Repository = Repository,
                Operation = operation,
                Target = target,
                CorrelationId =
                    "secure-tool-test",
                Timeout =
                    TimeSpan.FromSeconds(10)
            };

        private static string Git(
            string workingDirectory,
            params string[] arguments)
        {
            var startInfo =
                new ProcessStartInfo
                {
                    FileName =
                        TestToolResolver.Git(),
                    WorkingDirectory =
                        workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

            foreach (var argument
                     in arguments)
            {
                startInfo.ArgumentList.Add(
                    argument);
            }

            using var process =
                Process.Start(startInfo)
                ?? throw new InvalidOperationException(
                    "Unable to start temporary git.");

            var stdout =
                process.StandardOutput
                    .ReadToEnd();

            var stderr =
                process.StandardError
                    .ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Temporary git operation failed: {stderr}");
            }

            return stdout;
        }

        public void Dispose()
        {
            try
            {
                Git(
                    AuthoritativeRepository,
                    "worktree",
                    "prune");
            }
            catch
            {
            }

            if (Directory.Exists(container))
            {
                foreach (var file in
                    Directory.EnumerateFiles(
                        container,
                        "*",
                        SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(
                            file,
                            FileAttributes.Normal);
                    }
                    catch
                    {
                    }
                }

                Directory.Delete(
                    container,
                    recursive: true);
            }
        }
    }
    [Fact]
    public async Task Context_git_index_executes_from_fixed_allowlist()
    {
        using var fixture = new Fixture();

        var result =
            await fixture.Executor.ExecuteAsync(
                fixture.Request(
                    SecureToolOperation.GitListIndex));

        Assert.True(result.IsSuccess);

        Assert.Contains(
            "tracked.txt",
            result.StandardOutput,
            StringComparison.Ordinal);

        Assert.Contains(
            '\0',
            result.StandardOutput);
    }

    [Fact]
    public async Task Context_git_untracked_executes_from_fixed_allowlist()
    {
        using var fixture = new Fixture();

        string untracked =
            Path.Combine(
                fixture.Repository.RepositoryPath,
                "context-untracked.txt");

        await File.WriteAllTextAsync(
            untracked,
            "untracked");

        var result =
            await fixture.Executor.ExecuteAsync(
                fixture.Request(
                    SecureToolOperation.GitListUntracked));

        Assert.True(result.IsSuccess);

        Assert.Contains(
            "context-untracked.txt",
            result.StandardOutput,
            StringComparison.Ordinal);

        Assert.Contains(
            '\0',
            result.StandardOutput);
    }

    [Theory]
    [InlineData(SecureToolOperation.GitListIndex)]
    [InlineData(SecureToolOperation.GitListUntracked)]
    public async Task Context_git_operations_reject_user_target(
        SecureToolOperation operation)
    {
        using var fixture = new Fixture();

        var result =
            await fixture.Executor.ExecuteAsync(
                fixture.Request(
                    operation,
                    "--dangerous-user-input"));

        Assert.Equal(
            ToolExecutionOutcome.Rejected,
            result.Outcome);

        Assert.Equal(
            "TOOL_TARGET_NOT_ALLOWED",
            result.ErrorCode);
    }

}
