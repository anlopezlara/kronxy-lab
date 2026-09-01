using System.Diagnostics;
using System.Text;

namespace Kronxy.Infrastructure.Git;

internal sealed class SecureGitExecutor :
    ISecureGitExecutor
{
    private static readonly TimeSpan MaximumTimeout =
        TimeSpan.FromMinutes(5);

    private const int MaximumOutputBytes =
        8 * 1024 * 1024;

    private readonly string gitExecutable;

    public SecureGitExecutor(
        string gitExecutable)
    {
        if (string.IsNullOrWhiteSpace(
                gitExecutable) ||
            gitExecutable.IndexOfAny(
                ['\0', '\r', '\n']) >= 0 ||
            !Path.IsPathFullyQualified(
                gitExecutable))
        {
            throw new ArgumentException(
                "Git executable must be an absolute path.",
                nameof(gitExecutable));
        }

        string fullPath =
            Path.GetFullPath(
                gitExecutable);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "Git executable was not found.",
                fullPath);
        }

        this.gitExecutable =
            fullPath;
    }

    public async Task<SecureGitResult> ExecuteAsync(
        SecureGitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result(
                SecureGitOutcome.Cancelled,
                null,
                string.Empty,
                string.Empty,
                "SECURE_GIT_CANCELLED");
        }

        if (!ValidateLimits(request))
        {
            return Result(
                SecureGitOutcome.Rejected,
                null,
                string.Empty,
                string.Empty,
                "SECURE_GIT_INVALID_LIMITS");
        }

        SecureGitInvocation? invocation =
            SecureGitInvocationBuilder.Build(
                request);

        if (invocation is null)
        {
            return Result(
                SecureGitOutcome.Rejected,
                null,
                string.Empty,
                string.Empty,
                "SECURE_GIT_REQUEST_REJECTED");
        }

        if (!Directory.Exists(
                invocation.WorkingDirectory))
        {
            return Result(
                SecureGitOutcome.Rejected,
                null,
                string.Empty,
                string.Empty,
                "SECURE_GIT_WORKING_DIRECTORY_NOT_FOUND");
        }

        ProcessStartInfo startInfo =
            CreateStartInfo(
                invocation);

        using Process process =
            new()
            {
                StartInfo = startInfo
            };

        try
        {
            if (!process.Start())
            {
                return Result(
                    SecureGitOutcome.StartFailure,
                    null,
                    string.Empty,
                    string.Empty,
                    "SECURE_GIT_START_FAILED");
            }
        }
        catch (
            Exception exception)
            when (
                exception is
                    InvalidOperationException or
                    System.ComponentModel.Win32Exception)
        {
            return Result(
                SecureGitOutcome.StartFailure,
                null,
                string.Empty,
                string.Empty,
                "SECURE_GIT_START_FAILED");
        }

        using CancellationTokenSource timeout =
            new(request.Timeout);

        using CancellationTokenSource linked =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken,
                    timeout.Token);

        Task<BoundedReadResult> stdoutTask =
            ReadBoundedAsync(
                process.StandardOutput.BaseStream,
                request.StandardOutputLimitBytes,
                linked.Token);

        Task<BoundedReadResult> stderrTask =
            ReadBoundedAsync(
                process.StandardError.BaseStream,
                request.StandardErrorLimitBytes,
                linked.Token);

        try
        {
            await process
                .WaitForExitAsync(
                    linked.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKillProcessTree(
                process);

            try
            {
                await process
                    .WaitForExitAsync()
                    .ConfigureAwait(false);
            }
            catch
            {
            }

            return Result(
                cancellationToken.IsCancellationRequested
                    ? SecureGitOutcome.Cancelled
                    : SecureGitOutcome.TimedOut,
                process.HasExited
                    ? process.ExitCode
                    : null,
                string.Empty,
                string.Empty,
                cancellationToken.IsCancellationRequested
                    ? "SECURE_GIT_CANCELLED"
                    : "SECURE_GIT_TIMED_OUT");
        }

        BoundedReadResult stdout =
            await stdoutTask.ConfigureAwait(false);

        BoundedReadResult stderr =
            await stderrTask.ConfigureAwait(false);

        if (stdout.LimitExceeded ||
            stderr.LimitExceeded)
        {
            return Result(
                SecureGitOutcome.OutputLimitExceeded,
                process.ExitCode,
                stdout.Text,
                stderr.Text,
                "SECURE_GIT_OUTPUT_LIMIT_EXCEEDED");
        }

        if (process.ExitCode != 0)
        {
            return Result(
                SecureGitOutcome.NonZeroExitCode,
                process.ExitCode,
                stdout.Text,
                stderr.Text,
                "SECURE_GIT_NONZERO_EXIT");
        }

        return Result(
            SecureGitOutcome.Completed,
            process.ExitCode,
            stdout.Text,
            stderr.Text,
            string.Empty);
    }

    internal ProcessStartInfo CreateStartInfo(
        SecureGitInvocation invocation)
    {
        ProcessStartInfo startInfo =
            new()
            {
                FileName =
                    gitExecutable,

                WorkingDirectory =
                    invocation.WorkingDirectory,

                UseShellExecute =
                    false,

                RedirectStandardOutput =
                    true,

                RedirectStandardError =
                    true,

                CreateNoWindow =
                    true,

                StandardOutputEncoding =
                    Encoding.UTF8,

                StandardErrorEncoding =
                    Encoding.UTF8
            };

        startInfo.Environment.Clear();

        startInfo.Environment["PATH"] =
            Environment.GetEnvironmentVariable(
                "PATH")
            ?? "/usr/local/bin:/usr/bin:/bin";

        startInfo.Environment["LANG"] =
            "C.UTF-8";

        startInfo.Environment["LC_ALL"] =
            "C.UTF-8";

        startInfo.Environment[
            "GIT_TERMINAL_PROMPT"] =
            "0";

        startInfo.Environment[
            "GCM_INTERACTIVE"] =
            "Never";

        startInfo.Environment["GIT_PAGER"] =
            "cat";

        startInfo.Environment[
            "GIT_OPTIONAL_LOCKS"] =
            "0";

        startInfo.Environment[
            "GIT_CONFIG_NOSYSTEM"] =
            "1";

        startInfo.Environment[
            "GIT_CONFIG_GLOBAL"] =
            "/dev/null";

        startInfo.ArgumentList.Add(
            "--no-pager");

        startInfo.ArgumentList.Add(
            "-c");

        startInfo.ArgumentList.Add(
            "color.ui=false");

        startInfo.ArgumentList.Add(
            "-c");

        startInfo.ArgumentList.Add(
            "core.pager=cat");

        startInfo.ArgumentList.Add(
            "-c");

        startInfo.ArgumentList.Add(
            "core.fsmonitor=false");

        startInfo.ArgumentList.Add(
            "-c");

        startInfo.ArgumentList.Add(
            "credential.interactive=never");

        foreach (string argument
                 in invocation.Arguments)
        {
            startInfo.ArgumentList.Add(
                argument);
        }

        return startInfo;
    }

    private static bool ValidateLimits(
        SecureGitRequest request)
    {
        return
            request is not null &&
            request.Timeout > TimeSpan.Zero &&
            request.Timeout <= MaximumTimeout &&
            request.StandardOutputLimitBytes > 0 &&
            request.StandardErrorLimitBytes > 0 &&
            request.StandardOutputLimitBytes <=
                MaximumOutputBytes &&
            request.StandardErrorLimitBytes <=
                MaximumOutputBytes;
    }

    private static async Task<BoundedReadResult>
        ReadBoundedAsync(
            Stream stream,
            int maximumBytes,
            CancellationToken cancellationToken)
    {
        using MemoryStream output =
            new();

        byte[] buffer =
            new byte[8192];

        bool exceeded =
            false;

        while (true)
        {
            int count =
                await stream
                    .ReadAsync(
                        buffer.AsMemory(),
                        cancellationToken)
                    .ConfigureAwait(false);

            if (count == 0)
            {
                break;
            }

            int remaining =
                maximumBytes -
                checked((int)output.Length);

            if (remaining > 0)
            {
                int writeCount =
                    Math.Min(
                        remaining,
                        count);

                await output
                    .WriteAsync(
                        buffer.AsMemory(
                            0,
                            writeCount),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (count > remaining)
            {
                exceeded =
                    true;
            }
        }

        return new BoundedReadResult(
            Encoding.UTF8.GetString(
                output.ToArray()),
            exceeded);
    }

    private static void TryKillProcessTree(
        Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(
                    entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private static SecureGitResult Result(
        SecureGitOutcome outcome,
        int? exitCode,
        string stdout,
        string stderr,
        string errorCode)
    {
        return new SecureGitResult(
            outcome,
            exitCode,
            stdout,
            stderr,
            errorCode);
    }

    private sealed record BoundedReadResult(
        string Text,
        bool LimitExceeded);
}
