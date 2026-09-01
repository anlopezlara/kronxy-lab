using System.Diagnostics;
using Kronxy.Infrastructure.Execution;
using Kronxy.Infrastructure.Git;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class ExecutionPlaneInfrastructureTests
{
    [Fact]
    public async Task Source_revision_provider_returns_exact_repository_head()
    {
        using TemporaryRepository repository =
            new();

        GitSourceRevisionProvider provider =
            new(
                repository.Path,
                ResolveGit());

        var result =
            await provider
                .GetAuthoritativeHeadAsync();

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            repository.Head,
            result.Head);
    }

    [Fact]
    public void Options_reject_workspace_inside_authoritative_repository()
    {
        using TemporaryRepository repository =
            new();

        ExecutionPlaneOptions options =
            new()
            {
                RepositoryRoot =
                    repository.Path,

                WorkspaceRoot =
                    System.IO.Path.Combine(
                        repository.Path,
                        "workspaces"),

                DotnetExecutable =
                    ResolveDotnet(),

                GitExecutable =
                    ResolveGit(),

                DotnetTarget =
                    "Kronxy.sln"
            };

        Assert.Throws<
            InvalidOperationException>(
                options.Validate);
    }

    [Fact]
    public void Options_accept_separate_absolute_roots()
    {
        using TemporaryRepository repository =
            new();

        string workspace =
            System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "kronxy-options-tests",
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            ExecutionPlaneOptions options =
                new()
                {
                    RepositoryRoot =
                        repository.Path,

                    WorkspaceRoot =
                        workspace,

                    DotnetExecutable =
                        ResolveDotnet(),

                    GitExecutable =
                        ResolveGit(),

                    DotnetTarget =
                        "Kronxy.sln"
                };

            options.Validate();
        }
        finally
        {
            if (Directory.Exists(
                    workspace))
            {
                Directory.Delete(
                    workspace,
                    recursive: true);
            }
        }
    }

    [Fact]
    public void Options_reject_relative_executable()
    {
        using TemporaryRepository repository =
            new();

        ExecutionPlaneOptions options =
            new()
            {
                RepositoryRoot =
                    repository.Path,

                WorkspaceRoot =
                    System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(),
                        Guid.NewGuid()
                            .ToString("N")),

                DotnetExecutable =
                    "dotnet",

                GitExecutable =
                    ResolveGit(),

                DotnetTarget =
                    "Kronxy.sln"
            };

        Assert.Throws<
            InvalidOperationException>(
                options.Validate);
    }

    private static string ResolveDotnet()
    {
        string? hostPath =
            Environment.GetEnvironmentVariable(
                "DOTNET_HOST_PATH");

        if (!string.IsNullOrWhiteSpace(hostPath) &&
            Path.IsPathFullyQualified(hostPath) &&
            File.Exists(hostPath))
        {
            return Path.GetFullPath(hostPath);
        }

        string? dotnetRoot =
            Environment.GetEnvironmentVariable(
                "DOTNET_ROOT");

        if (!string.IsNullOrWhiteSpace(dotnetRoot))
        {
            string candidate =
                Path.Combine(
                    dotnetRoot,
                    OperatingSystem.IsWindows()
                        ? "dotnet.exe"
                        : "dotnet");

            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return ResolveFromPath(
            OperatingSystem.IsWindows()
                ? "dotnet.exe"
                : "dotnet");
    }

    private static string ResolveGit()
    {
        return ResolveFromPath(
            OperatingSystem.IsWindows()
                ? "git.exe"
                : "git");
    }

    private static string ResolveFromPath(
        string executable)
    {
        string? pathValue =
            Environment.GetEnvironmentVariable(
                "PATH");

        if (string.IsNullOrWhiteSpace(pathValue))
        {
            throw new InvalidOperationException(
                $"PATH is unavailable while resolving {executable}.");
        }

        foreach (string directory in
                 pathValue.Split(
                     Path.PathSeparator,
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            string candidate =
                Path.Combine(
                    directory,
                    executable);

            if (File.Exists(candidate))
            {
                return Path.GetFullPath(
                    candidate);
            }
        }

        throw new InvalidOperationException(
            $"{executable} executable not found.");
    }

    private sealed class TemporaryRepository :
        IDisposable
    {
        private readonly string container;

        public TemporaryRepository()
        {
            container =
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "kronxy-source-revision-tests",
                    Guid.NewGuid()
                        .ToString("N"));

            Path =
                System.IO.Path.Combine(
                    container,
                    "repository");

            Directory.CreateDirectory(
                Path);

            Git(
                "init",
                "-b",
                "main");

            Git(
                "config",
                "user.name",
                "KRONXY Tests");

            Git(
                "config",
                "user.email",
                "kronxy@example.invalid");

            File.WriteAllText(
                System.IO.Path.Combine(
                    Path,
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

            Head =
                Git(
                    "rev-parse",
                    "HEAD")
                .Trim()
                .ToLowerInvariant();
        }

        public string Path { get; }

        public string Head { get; }

        private string Git(
            params string[] arguments)
        {
            ProcessStartInfo start =
                new()
                {
                    FileName =
                        ResolveGit(),

                    WorkingDirectory =
                        Path,

                    UseShellExecute =
                        false,

                    RedirectStandardOutput =
                        true,

                    RedirectStandardError =
                        true,

                    CreateNoWindow =
                        true
                };

            foreach (
                string argument
                in arguments)
            {
                start.ArgumentList.Add(
                    argument);
            }

            using Process process =
                Process.Start(start)
                ?? throw new InvalidOperationException(
                    "git start failed.");

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
            if (Directory.Exists(
                    container))
            {
                Directory.Delete(
                    container,
                    recursive: true);
            }
        }
    }
}
