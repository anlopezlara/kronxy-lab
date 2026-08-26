using System.Diagnostics;
using System.Text;

namespace Kronxy.Context.Processes;

public sealed class ProcessRunner : IProcessRunner
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public async Task<ProcessResult> RunAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        using var timeoutSource = new CancellationTokenSource(request.Timeout);
        using var waitSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);
        using var captureSource = new CancellationTokenSource();
        using var process = new Process { StartInfo = CreateStartInfo(request) };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("El proceso no pudo iniciarse.");
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new ProcessRunnerException("No fue posible iniciar el proceso externo.");
        }

        var termination = (int)ProcessTerminationCause.Completed;
        void StopForLimit(ProcessTerminationCause cause)
        {
            if (Interlocked.CompareExchange(ref termination, (int)cause, (int)ProcessTerminationCause.Completed) ==
                (int)ProcessTerminationCause.Completed)
            {
                TryKillProcessTree(process);
            }
        }

        var stdoutTask = ReadBoundedAsync(
            process.StandardOutput.BaseStream,
            request.StandardOutputLimitBytes,
            () => StopForLimit(ProcessTerminationCause.StandardOutputLimitExceeded),
            captureSource.Token);
        var stderrTask = ReadBoundedAsync(
            process.StandardError.BaseStream,
            request.StandardErrorLimitBytes,
            () => StopForLimit(ProcessTerminationCause.StandardErrorLimitExceeded),
            captureSource.Token);

        var completionTask = WaitForCompletionAsync(process, stdoutTask, stderrTask);
        string[] outputs;

        try
        {
            outputs = await completionTask.WaitAsync(waitSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await StopAndCleanupAsync(process, captureSource, stdoutTask, stderrTask).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        catch (OperationCanceledException)
        {
            Interlocked.CompareExchange(
                ref termination,
                (int)ProcessTerminationCause.TimedOut,
                (int)ProcessTerminationCause.Completed);
            outputs = await StopAndCleanupAsync(process, captureSource, stdoutTask, stderrTask).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        stopwatch.Stop();

        var cause = (ProcessTerminationCause)Volatile.Read(ref termination);
        int? exitCode = process.HasExited ? process.ExitCode : null;
        if (cause == ProcessTerminationCause.Completed && exitCode != 0)
        {
            cause = ProcessTerminationCause.NonZeroExitCode;
        }

        return new ProcessResult
        {
            ExitCode = exitCode,
            StandardOutput = outputs[0],
            StandardError = outputs[1],
            Duration = stopwatch.Elapsed,
            TerminationCause = cause
        };
    }

    internal static ProcessStartInfo CreateStartInfo(ProcessRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var variable in request.EnvironmentVariables)
        {
            startInfo.Environment[variable.Key] = variable.Value;
        }

        return startInfo;
    }

    private static async Task<string> ReadBoundedAsync(
        Stream stream,
        int limit,
        Action onLimitExceeded,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream(Math.Min(limit, 16_384));
        var chunk = new byte[8_192];
        var captureInterrupted = false;

        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                var remaining = limit - checked((int)buffer.Length);
                if (read > remaining)
                {
                    if (remaining > 0)
                    {
                        buffer.Write(chunk, 0, remaining);
                    }

                    captureInterrupted = true;
                    onLimitExceeded();
                    break;
                }

                buffer.Write(chunk, 0, read);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            captureInterrupted = true;
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
            captureInterrupted = true;
        }
        catch (IOException) when (cancellationToken.IsCancellationRequested)
        {
            captureInterrupted = true;
        }

        return DecodeUtf8(buffer, captureInterrupted);
    }

    private static string DecodeUtf8(MemoryStream buffer, bool mayEndWithPartialCharacter)
    {
        var length = checked((int)buffer.Length);
        var maximumTrim = mayEndWithPartialCharacter ? Math.Min(3, length) : 0;
        for (var trim = 0; trim <= maximumTrim; trim++)
        {
            try
            {
                return StrictUtf8.GetString(buffer.GetBuffer(), 0, length - trim);
            }
            catch (DecoderFallbackException) when (trim < maximumTrim)
            {
            }
            catch (DecoderFallbackException)
            {
                throw new ProcessRunnerException("El proceso produjo una salida UTF-8 no válida.");
            }
        }

        throw new ProcessRunnerException("El proceso produjo una salida UTF-8 no válida.");
    }

    private static async Task<string[]> WaitForCompletionAsync(
        Process process,
        Task<string> stdoutTask,
        Task<string> stderrTask)
    {
        await process.WaitForExitAsync().ConfigureAwait(false);
        return await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
    }

    private static async Task<string[]> StopAndCleanupAsync(
        Process process,
        CancellationTokenSource captureSource,
        Task<string> stdoutTask,
        Task<string> stderrTask)
    {
        TryKillProcessTree(process);
        captureSource.Cancel();
        CloseRedirectedStreams(process);
        await process.WaitForExitAsync().ConfigureAwait(false);
        return await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
    }

    private static void CloseRedirectedStreams(Process process)
    {
        try
        {
            process.StandardOutput.Close();
        }
        catch (InvalidOperationException)
        {
        }

        try
        {
            process.StandardError.Close();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // El proceso terminó entre la comprobación y la solicitud de terminación.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // El sistema operativo ya no permite controlar el proceso; se esperará su salida.
        }
    }
}
