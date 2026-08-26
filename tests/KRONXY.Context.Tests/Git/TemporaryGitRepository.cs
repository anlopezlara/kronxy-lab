using System.Text;
using Kronxy.Context.Processes;

namespace Kronxy.Context.Tests.Git;

internal sealed class TemporaryGitRepository : IDisposable
{
    private static readonly string TestRootPath = Path.Combine(Path.GetTempPath(), "kronxy-context-tests");
    private readonly ProcessRunner runner = new();
    private readonly string globalConfigPath;
    private readonly string hooksPath;

    public TemporaryGitRepository(bool initialize = true)
    {
        RootPath = Path.Combine(TestRootPath, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
        globalConfigPath = Path.Combine(RootPath, ".git", "test-global-config");
        hooksPath = Path.Combine(RootPath, ".git", "test-hooks");
        if (initialize)
        {
            var templatePath = Path.Combine(RootPath, ".empty-template");
            Directory.CreateDirectory(templatePath);
            Run("init", $"--template={templatePath}", "-b", "main");
            Directory.Delete(templatePath);
            Directory.CreateDirectory(hooksPath);
            File.WriteAllText(globalConfigPath, string.Empty);
            Run("config", "--local", "user.name", "KRONXY Context Tests");
            Run("config", "--local", "user.email", "kronxy-context-tests@example.invalid");
        }
    }

    public string RootPath { get; }

    public void Write(string relativePath, string content)
    {
        var path = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public void Delete(string relativePath) => File.Delete(Path.Combine(RootPath, relativePath));

    public void CommitAll(string message)
    {
        Run("add", "--all", "--");
        Run("commit", "-m", message);
    }

    public void Run(params string[] arguments)
    {
        if (Execute(arguments).ExitCode != 0)
        {
            throw new InvalidOperationException("No se pudo preparar el repositorio Git temporal.");
        }
    }

    public void RunAllowFailure(params string[] arguments) => Execute(arguments);

    private ProcessResult Execute(IReadOnlyList<string> operationArguments)
    {
        var arguments = new List<string>
        {
            "-c", $"core.hooksPath={hooksPath}",
            "-c", "commit.gpgSign=false",
            "-c", "tag.gpgSign=false"
        };
        arguments.AddRange(operationArguments);

        return runner.RunAsync(new ProcessRequest
        {
            FileName = "git",
            WorkingDirectory = RootPath,
            Arguments = arguments,
            Timeout = TimeSpan.FromSeconds(20),
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["GIT_CONFIG_NOSYSTEM"] = "1",
                ["GIT_CONFIG_GLOBAL"] = globalConfigPath,
                ["GIT_TERMINAL_PROMPT"] = "0",
                ["GCM_INTERACTIVE"] = "Never"
            }
        }).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(RootPath, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(RootPath, recursive: true);
        if (Directory.Exists(TestRootPath) && !Directory.EnumerateFileSystemEntries(TestRootPath).Any())
        {
            Directory.Delete(TestRootPath);
        }
    }
}
