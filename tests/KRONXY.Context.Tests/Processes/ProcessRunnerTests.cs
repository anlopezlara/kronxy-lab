using System.Text;
using Kronxy.Context.Processes;
using Xunit;

namespace Kronxy.Context.Tests.Processes;

public sealed class ProcessRunnerTests
{
    private readonly ProcessRunner runner = new();

    [Fact]
    public void CreateStartInfo_UsesSecureProcessConfigurationAndArgumentList()
    {
        var request = Request("echo-arguments", "one value", "$(literal)", "; also-literal");
        var startInfo = ProcessRunner.CreateStartInfo(request);

        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.True(startInfo.CreateNoWindow);
        Assert.Equal(request.Arguments, startInfo.ArgumentList.Cast<string>());
        Assert.Empty(startInfo.Arguments);
    }

    [Fact]
    public async Task RunAsync_PreservesSpacesAndShellCharactersAsLiteralArguments()
    {
        var values = new[] { "one value", "$(not-executed)", "; & | < >", "Unicode-ñ-文件" };
        var result = await runner.RunAsync(Request("echo-arguments", values));
        var returned = result.StandardOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => Encoding.UTF8.GetString(Convert.FromBase64String(value)))
            .ToArray();

        Assert.Equal(values, returned);
        Assert.Equal(ProcessTerminationCause.Completed, result.TerminationCause);
        Assert.Equal(0, result.ExitCode);
    }

    [Theory]
    [InlineData(0, ProcessTerminationCause.Completed)]
    [InlineData(23, ProcessTerminationCause.NonZeroExitCode)]
    public async Task RunAsync_CapturesBothStreamsAndExitCode(int exitCode, ProcessTerminationCause cause)
    {
        var result = await runner.RunAsync(Request("streams", "out-value", "error-value", exitCode.ToString()));

        Assert.Equal("out-value", result.StandardOutput);
        Assert.Equal("error-value", result.StandardError);
        Assert.Equal(exitCode, result.ExitCode);
        Assert.Equal(cause, result.TerminationCause);
    }

    [Fact]
    public async Task RunAsync_TimeoutTerminatesProcess()
    {
        var result = await runner.RunAsync(Request("delay", "5000") with { Timeout = TimeSpan.FromMilliseconds(100) });

        Assert.True(result.TimedOut);
        Assert.Equal(ProcessTerminationCause.TimedOut, result.TerminationCause);
        Assert.True(result.Duration < TimeSpan.FromSeconds(4));
    }

    [Fact]
    public async Task RunAsync_ExternalCancellationThrowsOperationCanceledException()
    {
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync(Request("delay", "5000"), source.Token));
    }

    [Fact]
    public async Task RunAsync_StdoutLimitIsAppliedDuringRead()
    {
        var result = await runner.RunAsync(Request("stdout", "100000") with { StandardOutputLimitBytes = 100 });

        Assert.Equal(ProcessTerminationCause.StandardOutputLimitExceeded, result.TerminationCause);
        Assert.Equal(100, Encoding.UTF8.GetByteCount(result.StandardOutput));
        Assert.True(result.OutputLimitExceeded);
    }

    [Fact]
    public async Task RunAsync_StderrLimitIsAppliedDuringRead()
    {
        var result = await runner.RunAsync(Request("stderr", "100000") with { StandardErrorLimitBytes = 100 });

        Assert.Equal(ProcessTerminationCause.StandardErrorLimitExceeded, result.TerminationCause);
        Assert.Equal(100, Encoding.UTF8.GetByteCount(result.StandardError));
        Assert.True(result.OutputLimitExceeded);
    }

    [Fact]
    public async Task RunAsync_ByteLimitDoesNotReturnPartialUtf8Character()
    {
        var result = await runner.RunAsync(Request("utf8", "3") with { StandardOutputLimitBytes = 5 });

        Assert.Equal(ProcessTerminationCause.StandardOutputLimitExceeded, result.TerminationCause);
        Assert.Equal("éé", result.StandardOutput);
        Assert.Equal(4, Encoding.UTF8.GetByteCount(result.StandardOutput));
        Assert.DoesNotContain('\uFFFD', result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_TimeoutAlsoBoundsReadersHeldOpenByDescendant()
    {
        var pidFile = Path.Combine(Path.GetTempPath(), $"kronxy-process-{Guid.NewGuid():N}.pid");
        try
        {
            var result = await runner.RunAsync(Request("spawn-child", "300", pidFile) with
            {
                Timeout = TimeSpan.FromMilliseconds(75)
            });

            Assert.Equal(ProcessTerminationCause.TimedOut, result.TerminationCause);
            Assert.True(result.Duration < TimeSpan.FromSeconds(2));
            await Task.Delay(400);
            var childId = int.Parse(await File.ReadAllTextAsync(pidFile), System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(ProcessHasExited(childId));
        }
        finally
        {
            File.Delete(pidFile);
        }
    }

    [Fact]
    public async Task RunAsync_ConsumesLargeStdoutAndStderrConcurrentlyWithoutDeadlock()
    {
        const int size = 200_000;
        var result = await runner.RunAsync(Request("both", size.ToString()) with
        {
            StandardOutputLimitBytes = size + 1,
            StandardErrorLimitBytes = size + 1
        });

        Assert.Equal(ProcessTerminationCause.Completed, result.TerminationCause);
        Assert.Equal(size, result.StandardOutput.Length);
        Assert.Equal(size, result.StandardError.Length);
    }

    [Fact]
    public async Task RunAsync_StartFailureDoesNotExposeArguments()
    {
        const string secret = "super-sensitive-token";
        var exception = await Assert.ThrowsAsync<ProcessRunnerException>(() =>
            runner.RunAsync(new ProcessRequest
            {
                FileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
                WorkingDirectory = Path.GetTempPath(),
                Arguments = [secret]
            }));

        Assert.DoesNotContain(secret, exception.ToString(), StringComparison.Ordinal);
    }

    private static ProcessRequest Request(string mode, params string[] arguments)
    {
        var testProjectDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var hostDirectory = Path.Combine(testProjectDirectory, "Processes", "ProcessTestHost", "bin", configuration, "net8.0");
        var executable = Path.Combine(hostDirectory, OperatingSystem.IsWindows() ? "ProcessTestHost.exe" : "ProcessTestHost");

        return new ProcessRequest
        {
            FileName = executable,
            WorkingDirectory = hostDirectory,
            Arguments = [mode, .. arguments],
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    private static bool ProcessHasExited(int processId)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return process.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }
}
