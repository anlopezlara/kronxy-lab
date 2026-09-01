using System.Collections.Concurrent;
using System.Text.Json;
using Kronxy.Application.Workspaces;

namespace Kronxy.Infrastructure.Workspaces;

public sealed class WorkspaceManager : IWorkspaceManager
{
    private const string MarkerFileName = ".kronxy-workspace.json";
    private const int MarkerVersion = 1;

    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> JobLocks = new();

    private readonly string workspaceRoot;

    public WorkspaceManager(string workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot) ||
            !Path.IsPathFullyQualified(workspaceRoot) ||
            workspaceRoot.IndexOfAny(['\0', '\r', '\n']) >= 0)
        {
            throw new ArgumentException(
                "The workspace root must be a valid absolute path.",
                nameof(workspaceRoot));
        }

        this.workspaceRoot =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(workspaceRoot));
    }

    public async Task<WorkspaceOperationResult> PrepareAsync(
        Guid jobId,
        string jobExternalId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetWorkspacePath(
                jobId,
                jobExternalId,
                out var path,
                out var normalizedExternalId))
        {
            return WorkspaceOperationResult.Failure(
                WorkspaceFailureKind.InvalidJobIdentity,
                "WORKSPACE_INVALID_JOB_IDENTITY");
        }

        var gate = JobLocks.GetOrAdd(
            jobId,
            static _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!EnsureSafeRoot())
            {
                return WorkspaceOperationResult.Failure(
                    WorkspaceFailureKind.UnsafeRoot,
                    "WORKSPACE_ROOT_UNSAFE");
            }

            if (Directory.Exists(path))
            {
                if (!ValidateOwnedWorkspace(
                        path,
                        jobId,
                        normalizedExternalId))
                {
                    return WorkspaceOperationResult.Failure(
                        WorkspaceFailureKind.Collision,
                        "WORKSPACE_EXISTING_PATH_NOT_OWNED");
                }

                return WorkspaceOperationResult.Success(
                    new WorkspaceHandle(
                        jobId,
                        normalizedExternalId,
                        path,
                        WorkspaceOperationKind.Existing));
            }

            try
            {
                Directory.CreateDirectory(path);

                if (IsLink(new DirectoryInfo(path)))
                {
                    return WorkspaceOperationResult.Failure(
                        WorkspaceFailureKind.UnsafePath,
                        "WORKSPACE_PATH_IS_LINK");
                }

                WriteMarker(
                    path,
                    jobId,
                    normalizedExternalId);

                return WorkspaceOperationResult.Success(
                    new WorkspaceHandle(
                        jobId,
                        normalizedExternalId,
                        path,
                        WorkspaceOperationKind.Created));
            }
            catch (IOException)
            {
                TryRemoveEmptyDirectory(path);

                return WorkspaceOperationResult.Failure(
                    WorkspaceFailureKind.IoFailure,
                    "WORKSPACE_CREATE_IO_FAILURE");
            }
            catch (UnauthorizedAccessException)
            {
                TryRemoveEmptyDirectory(path);

                return WorkspaceOperationResult.Failure(
                    WorkspaceFailureKind.IoFailure,
                    "WORKSPACE_CREATE_ACCESS_DENIED");
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<WorkspaceOperationResult> RecoverAsync(
        Guid jobId,
        string jobExternalId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetWorkspacePath(
                jobId,
                jobExternalId,
                out var path,
                out var normalizedExternalId))
        {
            return WorkspaceOperationResult.Failure(
                WorkspaceFailureKind.InvalidJobIdentity,
                "WORKSPACE_INVALID_JOB_IDENTITY");
        }

        var gate = JobLocks.GetOrAdd(
            jobId,
            static _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!EnsureSafeRoot())
            {
                return WorkspaceOperationResult.Failure(
                    WorkspaceFailureKind.UnsafeRoot,
                    "WORKSPACE_ROOT_UNSAFE");
            }

            if (!Directory.Exists(path))
            {
                return WorkspaceOperationResult.Failure(
                    WorkspaceFailureKind.OwnershipMismatch,
                    "WORKSPACE_NOT_FOUND");
            }

            if (!ValidateOwnedWorkspace(
                    path,
                    jobId,
                    normalizedExternalId))
            {
                return WorkspaceOperationResult.Failure(
                    WorkspaceFailureKind.OwnershipMismatch,
                    "WORKSPACE_RECOVERY_OWNERSHIP_MISMATCH");
            }

            return WorkspaceOperationResult.Success(
                new WorkspaceHandle(
                    jobId,
                    normalizedExternalId,
                    path,
                    WorkspaceOperationKind.Recovered));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<WorkspaceOperationResult> CleanupAsync(
        Guid jobId,
        string jobExternalId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetWorkspacePath(
                jobId,
                jobExternalId,
                out var path,
                out var normalizedExternalId))
        {
            return WorkspaceOperationResult.Failure(
                WorkspaceFailureKind.InvalidJobIdentity,
                "WORKSPACE_INVALID_JOB_IDENTITY");
        }

        var gate = JobLocks.GetOrAdd(
            jobId,
            static _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!EnsureSafeRoot())
            {
                return WorkspaceOperationResult.Failure(
                    WorkspaceFailureKind.UnsafeRoot,
                    "WORKSPACE_ROOT_UNSAFE");
            }

            if (!Directory.Exists(path))
            {
                return WorkspaceOperationResult.Success(
                    new WorkspaceHandle(
                        jobId,
                        normalizedExternalId,
                        path,
                        WorkspaceOperationKind.AlreadyAbsent));
            }

            if (!ValidateOwnedWorkspace(
                    path,
                    jobId,
                    normalizedExternalId))
            {
                return WorkspaceOperationResult.Failure(
                    WorkspaceFailureKind.OwnershipMismatch,
                    "WORKSPACE_CLEANUP_OWNERSHIP_MISMATCH");
            }

            if (!TreeContainsNoLinks(path))
            {
                return WorkspaceOperationResult.Failure(
                    WorkspaceFailureKind.UnsafePath,
                    "WORKSPACE_CLEANUP_LINK_DETECTED");
            }

            try
            {
                Directory.Delete(path, recursive: true);

                return WorkspaceOperationResult.Success(
                    new WorkspaceHandle(
                        jobId,
                        normalizedExternalId,
                        path,
                        WorkspaceOperationKind.Removed));
            }
            catch (IOException)
            {
                return WorkspaceOperationResult.Failure(
                    WorkspaceFailureKind.IoFailure,
                    "WORKSPACE_CLEANUP_IO_FAILURE");
            }
            catch (UnauthorizedAccessException)
            {
                return WorkspaceOperationResult.Failure(
                    WorkspaceFailureKind.IoFailure,
                    "WORKSPACE_CLEANUP_ACCESS_DENIED");
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private bool TryGetWorkspacePath(
        Guid jobId,
        string jobExternalId,
        out string workspacePath,
        out string normalizedExternalId)
    {
        workspacePath = string.Empty;
        normalizedExternalId = string.Empty;

        if (jobId == Guid.Empty ||
            !IsValidExternalId(jobExternalId))
        {
            return false;
        }

        normalizedExternalId =
            jobExternalId.Trim().ToUpperInvariant();

        var directoryName =
            $"{normalizedExternalId.ToLowerInvariant()}-{jobId:N}";

        var candidate =
            Path.GetFullPath(
                Path.Combine(workspaceRoot, directoryName));

        if (!IsDirectChild(workspaceRoot, candidate))
        {
            return false;
        }

        workspacePath = candidate;
        return true;
    }

    private bool EnsureSafeRoot()
    {
        try
        {
            Directory.CreateDirectory(workspaceRoot);

            var current = new DirectoryInfo(workspaceRoot);

            while (current is not null)
            {
                if (IsLink(current))
                {
                    return false;
                }

                current = current.Parent;
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private bool ValidateOwnedWorkspace(
        string workspacePath,
        Guid jobId,
        string jobExternalId)
    {
        try
        {
            if (!IsDirectChild(workspaceRoot, workspacePath))
            {
                return false;
            }

            var workspaceInfo = new DirectoryInfo(workspacePath);

            if (!workspaceInfo.Exists ||
                IsLink(workspaceInfo))
            {
                return false;
            }

            var markerPath =
                Path.Combine(workspacePath, MarkerFileName);

            var markerInfo = new FileInfo(markerPath);

            if (!markerInfo.Exists ||
                IsLink(markerInfo))
            {
                return false;
            }

            using var stream = new FileStream(
                markerPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.SequentialScan);

            var marker =
                JsonSerializer.Deserialize<WorkspaceMarker>(stream);

            return marker is not null &&
                   marker.Version == MarkerVersion &&
                   marker.JobId == jobId &&
                   string.Equals(
                       marker.JobExternalId,
                       jobExternalId,
                       StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void WriteMarker(
        string workspacePath,
        Guid jobId,
        string jobExternalId)
    {
        var markerPath =
            Path.Combine(workspacePath, MarkerFileName);

        using var stream = new FileStream(
            markerPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.WriteThrough);

        JsonSerializer.Serialize(
            stream,
            new WorkspaceMarker(
                MarkerVersion,
                jobId,
                jobExternalId));

        stream.Flush(flushToDisk: true);
    }

    private static bool TreeContainsNoLinks(string root)
    {
        try
        {
            var pending = new Stack<DirectoryInfo>();
            pending.Push(new DirectoryInfo(root));

            while (pending.Count > 0)
            {
                var directory = pending.Pop();

                if (IsLink(directory))
                {
                    return false;
                }

                foreach (var entry in directory.EnumerateFileSystemInfos())
                {
                    if (IsLink(entry))
                    {
                        return false;
                    }

                    if (entry is DirectoryInfo childDirectory)
                    {
                        pending.Push(childDirectory);
                    }
                }
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsDirectChild(
        string root,
        string candidate)
    {
        var normalizedRoot =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(root));

        var normalizedCandidate =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(candidate));

        var parent =
            Directory.GetParent(normalizedCandidate);

        return parent is not null &&
               string.Equals(
                   Path.TrimEndingDirectorySeparator(
                       parent.FullName),
                   normalizedRoot,
                   OperatingSystem.IsWindows()
                       ? StringComparison.OrdinalIgnoreCase
                       : StringComparison.Ordinal);
    }

    private static bool IsValidExternalId(
        string? externalId)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return false;
        }

        var value = externalId.Trim();

        if (!value.StartsWith(
                "KRX-",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var number = value.AsSpan(4);

        if (number.Length < 6 ||
            number.Length > 18)
        {
            return false;
        }

        foreach (var character in number)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
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
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static void TryRemoveEmptyDirectory(
        string path)
    {
        try
        {
            if (Directory.Exists(path) &&
                !Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record WorkspaceMarker(
        int Version,
        Guid JobId,
        string JobExternalId);
}
