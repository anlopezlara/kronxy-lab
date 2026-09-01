using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;

namespace Kronxy.Infrastructure.Execution;

public sealed record SecureToolExecutorOptions
{
    public required string DotnetExecutable { get; init; }

    public required string GitExecutable { get; init; }

    public string? TestDatabaseConnectionString { get; init; }

    public TimeSpan MaximumTimeout { get; init; } =
        TimeSpan.FromMinutes(30);

    public int MaximumOutputBytes { get; init; } =
        8 * 1024 * 1024;

    public static SecureToolExecutorOptions Create(
        string dotnetExecutable,
        string gitExecutable,
        string? testDatabaseConnectionString = null) =>
        new()
        {
            DotnetExecutable = dotnetExecutable,
            GitExecutable = gitExecutable,
            TestDatabaseConnectionString =
                testDatabaseConnectionString
        };
}

public sealed class SecureToolExecutor : ISecureToolExecutor
{
    private readonly SecureToolExecutorOptions options;

    public SecureToolExecutor(
        SecureToolExecutorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!IsValidExecutable(
                options.DotnetExecutable) ||
            !IsValidExecutable(
                options.GitExecutable))
        {
            throw new ArgumentException(
                "Secure tool executables are invalid.",
                nameof(options));
        }

        if (options.MaximumTimeout <= TimeSpan.Zero ||
            options.MaximumTimeout >
                TimeSpan.FromHours(24))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options));
        }

        if (options.MaximumOutputBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options));
        }

        this.options = options;
    }

    public async Task<SecureToolResult> ExecuteAsync(
        SecureToolRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var started = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        if (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            return CreateResult(
                request,
                started,
                stopwatch.Elapsed,
                ToolExecutionOutcome.Cancelled,
                null,
                string.Empty,
                string.Empty,
                "TOOL_CANCELLED");
        }

        var validation =
            ValidateRequest(request);

        if (validation is not null)
        {
            stopwatch.Stop();

            return CreateResult(
                request,
                started,
                stopwatch.Elapsed,
                ToolExecutionOutcome.Rejected,
                null,
                string.Empty,
                string.Empty,
                validation);
        }

        if (request.Operation ==
                SecureToolOperation.DotnetTest)
        {
            string? preparationFailure =
                PrepareTestResultsDirectory(
                    request);

            if (preparationFailure is not null)
            {
                stopwatch.Stop();

                return CreateResult(
                    request,
                    started,
                    stopwatch.Elapsed,
                    ToolExecutionOutcome.Rejected,
                    null,
                    string.Empty,
                    string.Empty,
                    preparationFailure);
            }
        }

        var invocation =
            BuildInvocation(request);

        if (invocation is null)
        {
            stopwatch.Stop();

            return CreateResult(
                request,
                started,
                stopwatch.Elapsed,
                ToolExecutionOutcome.Rejected,
                null,
                string.Empty,
                string.Empty,
                "TOOL_OPERATION_NOT_ALLOWED");
        }

        var startInfo =
            CreateStartInfo(
                request,
                invocation);

        using var process =
            new Process
            {
                StartInfo = startInfo
            };

        try
        {
            if (!process.Start())
            {
                stopwatch.Stop();

                return CreateResult(
                    request,
                    started,
                    stopwatch.Elapsed,
                    ToolExecutionOutcome.StartFailure,
                    null,
                    string.Empty,
                    string.Empty,
                    "TOOL_START_FAILED");
            }
        }
        catch (
            Exception exception)
            when (
                exception is
                    InvalidOperationException or
                    System.ComponentModel.Win32Exception)
        {
            stopwatch.Stop();

            return CreateResult(
                request,
                started,
                stopwatch.Elapsed,
                ToolExecutionOutcome.StartFailure,
                null,
                string.Empty,
                string.Empty,
                "TOOL_START_FAILED");
        }

        var termination =
            new TerminationState();

        using var timeoutSource =
            new CancellationTokenSource(
                request.Timeout);

        using var waitSource =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken,
                    timeoutSource.Token);

        using var captureSource =
            new CancellationTokenSource();

        void StopForOutputLimit()
        {
            if (termination.TrySet(
                    ToolExecutionOutcome
                        .OutputLimitExceeded))
            {
                TryKillProcessTree(process);
                captureSource.Cancel();
            }
        }

        var stdoutTask =
            ReadBoundedAsync(
                process.StandardOutput.BaseStream,
                request.StandardOutputLimitBytes,
                StopForOutputLimit,
                captureSource.Token);

        var stderrTask =
            ReadBoundedAsync(
                process.StandardError.BaseStream,
                request.StandardErrorLimitBytes,
                StopForOutputLimit,
                captureSource.Token);

        try
        {
            await process
                .WaitForExitAsync(
                    waitSource.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken
                .IsCancellationRequested)
            {
                termination.TrySet(
                    ToolExecutionOutcome.Cancelled);
            }
            else
            {
                termination.TrySet(
                    ToolExecutionOutcome.TimedOut);
            }

            TryKillProcessTree(process);
            captureSource.Cancel();

            try
            {
                await process
                    .WaitForExitAsync()
                    .ConfigureAwait(false);
            }
            catch
            {
            }
        }

        string stdout;
        string stderr;

        try
        {
            stdout =
                await stdoutTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            stdout = string.Empty;
        }

        try
        {
            stderr =
                await stderrTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            stderr = string.Empty;
        }

        stopwatch.Stop();

        var outcome =
            termination.Value;

        int? exitCode =
            process.HasExited
                ? process.ExitCode
                : null;

        if (outcome ==
            ToolExecutionOutcome.Completed)
        {
            outcome =
                exitCode == 0
                    ? ToolExecutionOutcome.Completed
                    : ToolExecutionOutcome
                        .NonZeroExitCode;
        }

        var errorCode =
            outcome switch
            {
                ToolExecutionOutcome.Completed =>
                    string.Empty,

                ToolExecutionOutcome
                    .NonZeroExitCode =>
                    "TOOL_NONZERO_EXIT",

                ToolExecutionOutcome.TimedOut =>
                    "TOOL_TIMED_OUT",

                ToolExecutionOutcome.Cancelled =>
                    "TOOL_CANCELLED",

                ToolExecutionOutcome
                    .OutputLimitExceeded =>
                    "TOOL_OUTPUT_LIMIT_EXCEEDED",

                _ =>
                    "TOOL_EXECUTION_FAILED"
            };

        return CreateResult(
            request,
            started,
            stopwatch.Elapsed,
            outcome,
            exitCode,
            stdout,
            stderr,
            errorCode);
    }

    internal ProcessStartInfo CreateStartInfo(
        SecureToolRequest request,
        ToolInvocation invocation)
    {
        var startInfo =
            new ProcessStartInfo
            {
                FileName =
                    invocation.Executable,
                WorkingDirectory =
                    request.Repository.RepositoryPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding =
                    Encoding.UTF8,
                StandardErrorEncoding =
                    Encoding.UTF8
            };

        startInfo.Environment.Clear();

        startInfo.Environment["PATH"] =
            "/usr/local/bin:/usr/bin:/bin";

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

        startInfo.Environment[
            "GIT_PAGER"] =
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

        startInfo.Environment[
            "DOTNET_NOLOGO"] =
            "1";

        startInfo.Environment[
            "DOTNET_CLI_TELEMETRY_OPTOUT"] =
            "1";

        startInfo.Environment[
            "DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] =
            "1";

        startInfo.Environment[
            "HOME"] =
            request.Repository.WorkspacePath;

        startInfo.Environment[
            "DOTNET_CLI_HOME"] =
            request.Repository.WorkspacePath;

        if (request.Operation ==
                SecureToolOperation.DotnetTest &&
            !string.IsNullOrWhiteSpace(
                options.TestDatabaseConnectionString))
        {
            startInfo.Environment[
                "ConnectionStrings__Database"] =
                options.TestDatabaseConnectionString;
        }

        foreach (var argument
                 in invocation.Arguments)
        {
            startInfo.ArgumentList.Add(
                argument);
        }

        return startInfo;
    }

    internal ToolInvocation? BuildInvocation(
        SecureToolRequest request)
    {
        return request.Operation switch
        {
            SecureToolOperation.DotnetRestore =>
                Dotnet(
                    "restore",
                    request.Target!),

            SecureToolOperation.DotnetBuild =>
                Dotnet(
                    "build",
                    request.Target!,
                    "--no-restore"),

            SecureToolOperation.DotnetTest =>
                Dotnet(
                    "test",
                    request.Target!,
                    "--no-build",
                    "--logger",
                    "trx",
                    "--results-directory",
                    GetTestResultsDirectory(
                        request)),

            SecureToolOperation.GitStatus =>
                Git(
                    "status",
                    "--porcelain=v1",
                    "--untracked-files=all"),

            SecureToolOperation.GitDiff =>
                Git(
                    "diff",
                    "--no-ext-diff",
                    "--no-textconv",
                    "HEAD",
                    "--"),

            SecureToolOperation.GitDiffStat =>
                Git(
                    "diff",
                    "--stat",
                    "--no-ext-diff",
                    "--no-textconv",
                    "HEAD",
                    "--"),

            SecureToolOperation.GitListIndex =>
                Git(
                    "ls-files",
                    "--stage",
                    "-z"),

            SecureToolOperation.GitListUntracked =>
                Git(
                    "ls-files",
                    "--others",
                    "--exclude-standard",
                    "-z"),

            _ => null
        };
    }

    private string? ValidateRequest(
        SecureToolRequest request)
    {
        if (request.Repository is null ||
            request.Repository.JobId == Guid.Empty)
        {
            return "TOOL_INVALID_REPOSITORY";
        }

        if (!Enum.IsDefined(
                request.Operation))
        {
            return "TOOL_OPERATION_NOT_ALLOWED";
        }

        if (request.Timeout <= TimeSpan.Zero ||
            request.Timeout >
                options.MaximumTimeout)
        {
            return "TOOL_INVALID_TIMEOUT";
        }

        if (request.StandardOutputLimitBytes <= 0 ||
            request.StandardErrorLimitBytes <= 0 ||
            request.StandardOutputLimitBytes >
                options.MaximumOutputBytes ||
            request.StandardErrorLimitBytes >
                options.MaximumOutputBytes)
        {
            return "TOOL_INVALID_OUTPUT_LIMIT";
        }

        if (request.CorrelationId.IndexOfAny(
                ['\0', '\r', '\n']) >= 0)
        {
            return "TOOL_INVALID_CORRELATION_ID";
        }

        string workspacePath;
        string repositoryPath;

        try
        {
            workspacePath =
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(
                        request.Repository
                            .WorkspacePath));

            repositoryPath =
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(
                        request.Repository
                            .RepositoryPath));
        }
        catch
        {
            return "TOOL_INVALID_REPOSITORY_PATH";
        }

        if (!Directory.Exists(
                workspacePath) ||
            !Directory.Exists(
                repositoryPath))
        {
            return "TOOL_REPOSITORY_NOT_FOUND";
        }

        if (!IsDirectChild(
                workspacePath,
                repositoryPath))
        {
            return "TOOL_REPOSITORY_OUTSIDE_WORKSPACE";
        }

        if (ContainsLinkInPath(
                workspacePath) ||
            IsLink(
                new DirectoryInfo(
                    repositoryPath)))
        {
            return "TOOL_SYMLINK_REJECTED";
        }

        if (!ValidateWorkspaceOwnership(
                request.Repository,
                workspacePath))
        {
            return "TOOL_WORKSPACE_OWNERSHIP_INVALID";
        }

        var isDotnet =
            request.Operation is
                SecureToolOperation.DotnetRestore or
                SecureToolOperation.DotnetBuild or
                SecureToolOperation.DotnetTest;

        if (isDotnet)
        {
            var targetError =
                ValidateTarget(
                    repositoryPath,
                    request.Target);

            if (targetError is not null)
            {
                return targetError;
            }

            if (request.Operation ==
                SecureToolOperation.DotnetTest)
            {
                var testResultsError =
                    ValidateTestResultsPath(
                        workspacePath,
                        request);

                if (testResultsError is not null)
                {
                    return testResultsError;
                }
            }
        }
        else if (request.Target is not null)
        {
            return "TOOL_TARGET_NOT_ALLOWED";
        }

        return null;
    }

    private static bool ValidateWorkspaceOwnership(
        RepositoryWorktreeHandle repository,
        string workspacePath)
    {
        try
        {
            var markerPath =
                Path.Combine(
                    workspacePath,
                    ".kronxy-workspace.json");

            var markerInfo =
                new FileInfo(markerPath);

            if (!markerInfo.Exists ||
                IsLink(markerInfo))
            {
                return false;
            }

            using var stream =
                new FileStream(
                    markerPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    FileOptions.SequentialScan);

            using var document =
                JsonDocument.Parse(stream);

            var root =
                document.RootElement;

            if (!root.TryGetProperty(
                    "Version",
                    out var version) ||
                version.ValueKind !=
                    JsonValueKind.Number ||
                version.GetInt32() != 1)
            {
                return false;
            }

            if (!root.TryGetProperty(
                    "JobId",
                    out var jobId) ||
                jobId.ValueKind !=
                    JsonValueKind.String ||
                !jobId.TryGetGuid(
                    out var markerJobId) ||
                markerJobId !=
                    repository.JobId)
            {
                return false;
            }

            if (!root.TryGetProperty(
                    "JobExternalId",
                    out var externalId) ||
                externalId.ValueKind !=
                    JsonValueKind.String)
            {
                return false;
            }

            return string.Equals(
                externalId.GetString(),
                repository.JobExternalId,
                StringComparison.Ordinal);
        }
        catch (
            Exception exception)
            when (
                exception is
                    JsonException or
                    IOException or
                    UnauthorizedAccessException or
                    InvalidOperationException or
                    FormatException)
        {
            return false;
        }
    }

    private static string? ValidateTarget(
        string repositoryPath,
        string? target)
    {
        if (string.IsNullOrWhiteSpace(target) ||
            target.IndexOfAny(
                [
                    '\0',
                    '\r',
                    '\n',
                    ';',
                    '&',
                    '|',
                    '<',
                    '>',
                    '$',
                    '`',
                    '"',
                    '\''
                ]) >= 0)
        {
            return "TOOL_TARGET_INVALID";
        }

        if (Path.IsPathFullyQualified(target) ||
            target.StartsWith('/') ||
            target.StartsWith('\\'))
        {
            return "TOOL_TARGET_ABSOLUTE_REJECTED";
        }

        var segments =
            target
                .Replace('\\', '/')
                .Split('/');

        if (segments.Any(
                segment =>
                    string.IsNullOrWhiteSpace(segment) ||
                    segment is "." or ".."))
        {
            return "TOOL_TARGET_TRAVERSAL_REJECTED";
        }

        var extension =
            Path.GetExtension(target);

        if (!extension.Equals(
                ".sln",
                StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(
                ".csproj",
                StringComparison.OrdinalIgnoreCase))
        {
            return "TOOL_TARGET_TYPE_NOT_ALLOWED";
        }

        string candidate;

        try
        {
            candidate =
                Path.GetFullPath(
                    Path.Combine(
                        repositoryPath,
                        target));
        }
        catch
        {
            return "TOOL_TARGET_INVALID";
        }

        if (!IsUnderRoot(
                repositoryPath,
                candidate))
        {
            return "TOOL_TARGET_OUTSIDE_REPOSITORY";
        }

        if (!File.Exists(candidate) ||
            ContainsLinkInPathFromRoot(
                repositoryPath,
                candidate))
        {
            return "TOOL_TARGET_NOT_SAFE";
        }

        return null;
    }

    private static string? ValidateTestResultsPath(
        string workspacePath,
        SecureToolRequest request)
    {
        try
        {
            var normalizedWorkspace =
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(
                        workspacePath));

            var kronxyDirectory =
                Path.GetFullPath(
                    Path.Combine(
                        normalizedWorkspace,
                        ".kronxy"));

            var resultsDirectory =
                Path.GetFullPath(
                    GetTestResultsDirectory(
                        request));

            if (!IsUnderRoot(
                    normalizedWorkspace,
                    kronxyDirectory) ||
                !IsUnderRoot(
                    normalizedWorkspace,
                    resultsDirectory))
            {
                return "TOOL_TEST_RESULTS_PATH_UNSAFE";
            }

            if (File.Exists(
                    kronxyDirectory))
            {
                return "TOOL_TEST_RESULTS_PATH_UNSAFE";
            }

            if (Directory.Exists(
                    kronxyDirectory) &&
                IsLink(
                    new DirectoryInfo(
                        kronxyDirectory)))
            {
                return "TOOL_TEST_RESULTS_PATH_UNSAFE";
            }

            if (File.Exists(
                    resultsDirectory))
            {
                return "TOOL_TEST_RESULTS_PATH_UNSAFE";
            }

            if (Directory.Exists(
                    resultsDirectory) &&
                IsLink(
                    new DirectoryInfo(
                        resultsDirectory)))
            {
                return "TOOL_TEST_RESULTS_PATH_UNSAFE";
            }

            return null;
        }
        catch
        {
            return "TOOL_TEST_RESULTS_PATH_UNSAFE";
        }
    }

    private static string? PrepareTestResultsDirectory(
        SecureToolRequest request)
    {
        try
        {
            string workspace =
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(
                        request.Repository.WorkspacePath));

            string kronxyDirectory =
                Path.GetFullPath(
                    Path.Combine(
                        workspace,
                        ".kronxy"));

            string resultsDirectory =
                Path.GetFullPath(
                    GetTestResultsDirectory(
                        request));

            if (!IsUnderRoot(
                    workspace,
                    kronxyDirectory) ||
                !IsUnderRoot(
                    workspace,
                    resultsDirectory))
            {
                return "TOOL_TEST_RESULTS_PATH_UNSAFE";
            }

            if (File.Exists(kronxyDirectory) ||
                File.Exists(resultsDirectory))
            {
                return "TOOL_TEST_RESULTS_PATH_UNSAFE";
            }

            if (Directory.Exists(kronxyDirectory) &&
                IsLink(
                    new DirectoryInfo(
                        kronxyDirectory)))
            {
                return "TOOL_TEST_RESULTS_PATH_UNSAFE";
            }

            if (Directory.Exists(resultsDirectory) &&
                IsLink(
                    new DirectoryInfo(
                        resultsDirectory)))
            {
                return "TOOL_TEST_RESULTS_PATH_UNSAFE";
            }

            Directory.CreateDirectory(
                resultsDirectory);

            DirectoryInfo resultsInfo =
                new(resultsDirectory);

            resultsInfo.Refresh();

            if (!resultsInfo.Exists ||
                IsLink(resultsInfo))
            {
                return "TOOL_TEST_RESULTS_PATH_UNSAFE";
            }

            foreach (FileSystemInfo entry
                     in resultsInfo
                         .EnumerateFileSystemInfos())
            {
                entry.Refresh();

                if (IsLink(entry) ||
                    entry is DirectoryInfo)
                {
                    return "TOOL_TEST_RESULTS_PATH_UNSAFE";
                }

                if (entry is not FileInfo file ||
                    !file.Extension.Equals(
                        ".trx",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return "TOOL_TEST_RESULTS_PATH_UNSAFE";
                }
            }

            foreach (FileInfo trx
                     in resultsInfo
                         .EnumerateFiles("*.trx"))
            {
                trx.Refresh();

                if (IsLink(trx))
                {
                    return "TOOL_TEST_RESULTS_PATH_UNSAFE";
                }

                trx.Delete();
            }

            return null;
        }
        catch (Exception exception)
            when (exception is
                    IOException or
                    UnauthorizedAccessException or
                    ArgumentException or
                    NotSupportedException)
        {
            return "TOOL_TEST_RESULTS_PATH_UNSAFE";
        }
    }

    internal static string GetTestResultsDirectory(
        SecureToolRequest request)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        ArgumentNullException.ThrowIfNull(
            request.Repository);

        return Path.Combine(
            request.Repository.WorkspacePath,
            ".kronxy",
            "test-results");
    }

    private ToolInvocation Dotnet(
        params string[] arguments) =>
        new(
            options.DotnetExecutable,
            arguments);

    private ToolInvocation Git(
        params string[] arguments) =>
        new(
            options.GitExecutable,
            arguments);

    private static async Task<string>
        ReadBoundedAsync(
            Stream stream,
            int limit,
            Action onLimitExceeded,
            CancellationToken cancellationToken)
    {
        using var buffer =
            new MemoryStream(
                Math.Min(
                    limit,
                    16_384));

        var chunk =
            new byte[8192];

        while (true)
        {
            int read;

            try
            {
                read =
                    await stream.ReadAsync(
                        chunk,
                        cancellationToken)
                        .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (IOException)
            {
                break;
            }

            if (read == 0)
            {
                break;
            }

            var remaining =
                limit -
                checked((int)buffer.Length);

            if (read > remaining)
            {
                if (remaining > 0)
                {
                    buffer.Write(
                        chunk,
                        0,
                        remaining);
                }

                onLimitExceeded();
                break;
            }

            buffer.Write(
                chunk,
                0,
                read);
        }

        return DecodeUtf8(buffer);
    }

    private static string DecodeUtf8(
        MemoryStream stream)
    {
        var bytes =
            stream.ToArray();

        while (bytes.Length > 0)
        {
            try
            {
                return new UTF8Encoding(
                        false,
                        true)
                    .GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                bytes =
                    bytes[..^1];
            }
        }

        return string.Empty;
    }

    private static SecureToolResult CreateResult(
        SecureToolRequest request,
        DateTime started,
        TimeSpan duration,
        ToolExecutionOutcome outcome,
        int? exitCode,
        string stdout,
        string stderr,
        string errorCode)
    {
        var ended =
            started + duration;

        return new SecureToolResult(
            outcome,
            exitCode,
            stdout,
            stderr,
            new ToolExecutionAudit(
                request.Repository.JobId,
                request.Repository.JobExternalId,
                request.Operation,
                request.Repository.WorkspacePath,
                request.Repository.RepositoryPath,
                request.CorrelationId,
                started,
                ended,
                duration,
                exitCode,
                outcome),
            errorCode);
    }

    private static bool IsValidExecutable(
        string? executable)
    {
        if (string.IsNullOrWhiteSpace(executable) ||
            executable.IndexOfAny(
                ['\0', '\r', '\n']) >= 0 ||
            !Path.IsPathFullyQualified(executable))
        {
            return false;
        }

        try
        {
            var fullPath =
                Path.GetFullPath(executable);

            return File.Exists(fullPath);
        }
        catch (
            Exception exception)
            when (
                exception is
                    ArgumentException or
                    NotSupportedException or
                    PathTooLongException)
        {
            return false;
        }
    }

    private static bool IsDirectChild(
        string parent,
        string child)
    {
        var childParent =
            Directory.GetParent(
                Path.GetFullPath(child));

        return childParent is not null &&
               PathEquals(
                   childParent.FullName,
                   parent);
    }

    private static bool IsUnderRoot(
        string root,
        string candidate)
    {
        var normalizedRoot =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(root));

        var normalizedCandidate =
            Path.GetFullPath(candidate);

        var prefix =
            normalizedRoot +
            Path.DirectorySeparatorChar;

        return normalizedCandidate.StartsWith(
            prefix,
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
    }

    private static bool PathEquals(
        string left,
        string right)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(right)),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
    }

    private static bool ContainsLinkInPath(
        string path)
    {
        try
        {
            var current =
                new DirectoryInfo(
                    Path.GetFullPath(path));

            while (current is not null)
            {
                if (IsLink(current))
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }
        catch
        {
            return true;
        }
    }

    private static bool ContainsLinkInPathFromRoot(
        string root,
        string candidate)
    {
        try
        {
            var relative =
                Path.GetRelativePath(
                    root,
                    candidate);

            var current =
                Path.GetFullPath(root);

            foreach (var segment in
                     relative.Split(
                         [
                             Path.DirectorySeparatorChar,
                             Path.AltDirectorySeparatorChar
                         ],
                         StringSplitOptions
                             .RemoveEmptyEntries))
            {
                current =
                    Path.Combine(
                        current,
                        segment);

                var info =
                    File.Exists(current)
                        ? (FileSystemInfo)
                            new FileInfo(current)
                        : new DirectoryInfo(current);

                if (IsLink(info))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return true;
        }
    }

    private static bool IsLink(
        FileSystemInfo info)
    {
        try
        {
            info.Refresh();

            return info.LinkTarget is not null ||
                   (info.Attributes &
                    FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return true;
        }
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

    internal sealed record ToolInvocation(
        string Executable,
        IReadOnlyList<string> Arguments);

    private sealed class TerminationState
    {
        private int value =
            (int)ToolExecutionOutcome.Completed;

        public ToolExecutionOutcome Value =>
            (ToolExecutionOutcome)
                Volatile.Read(ref value);

        public bool TrySet(
            ToolExecutionOutcome outcome)
        {
            return Interlocked.CompareExchange(
                       ref value,
                       (int)outcome,
                       (int)ToolExecutionOutcome.Completed) ==
                   (int)ToolExecutionOutcome.Completed;
        }
    }
}
