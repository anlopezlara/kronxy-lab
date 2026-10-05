using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Kronxy.Application.Execution;

namespace Kronxy.Infrastructure.Execution;

public interface ISafeChangeCommitObserver
{
    void AfterCommit(
        int operationIndex);
}

public sealed class NoOpSafeChangeCommitObserver :
    ISafeChangeCommitObserver
{
    public void AfterCommit(
        int operationIndex)
    {
    }
}

public sealed class SafeChangeApplier :
    ISafeChangeApplier
{
    private static readonly ConcurrentDictionary<
        Guid,
        SemaphoreSlim> JobLocks =
            new();

    private static readonly UTF8Encoding StrictUtf8 =
        new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    private readonly SafeChangeWorkspaceValidator validator;
    private readonly ISafeChangeCommitObserver observer;

    public SafeChangeApplier(
        SafeChangeWorkspaceValidator validator,
        ISafeChangeCommitObserver observer)
    {
        this.validator =
            validator ??
            throw new ArgumentNullException(
                nameof(validator));

        this.observer =
            observer ??
            throw new ArgumentNullException(
                nameof(observer));
    }

    public async Task<SafeChangeApplicationResult>
        ApplyAsync(
            SafeChangeApplicationRequest request,
            CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Failure(
                SafeChangeApplicationFailureKind.InvalidRequest,
                "SAFE_CHANGE_INVALID_REQUEST");
        }

        SemaphoreSlim gate =
            JobLocks.GetOrAdd(
                request.JobId,
                static _ =>
                    new SemaphoreSlim(
                        1,
                        1));

        try
        {
            await gate.WaitAsync(
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Failure(
                SafeChangeApplicationFailureKind.Cancelled,
                "SAFE_CHANGE_CANCELLED");
        }

        try
        {
            return await ApplyUnderLockAsync(
                    request,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<SafeChangeApplicationResult>
        ApplyUnderLockAsync(
            SafeChangeApplicationRequest request,
            CancellationToken cancellationToken)
    {
        DateTime startedOnUtc =
            DateTime.UtcNow;

        string transactionPath =
            GetTransactionPath(
                request);

        SafeChangeCompletionReceiptResult completion =
            await SafeChangeCompletionReceipt.InspectAsync(
                    request,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!completion.IsSuccess)
        {
            return Failure(
                completion.FailureKind,
                completion.ErrorCode);
        }

        if (completion.IsCompleted &&
            completion.Changes is not null)
        {
            if (PathEntryExists(transactionPath) &&
                !TryDeleteTransaction(transactionPath))
            {
                return Failure(
                    SafeChangeApplicationFailureKind.AtomicityFailure,
                    "SAFE_CHANGE_COMPLETED_TRANSACTION_CLEANUP_FAILED");
            }

            return SafeChangeApplicationResult.Success(
                new SafeChangeApplicationReport(
                    request.JobId,
                    request.RunId,
                    completion.Changes,
                    completion.Changes.Sum(
                        item => item.SizeBytes),
                    startedOnUtc,
                    DateTime.UtcNow));
        }

        SafeChangeJournalResult recovery =
            await SafeChangeTransactionJournal.RecoverAsync(
                    request,
                    transactionPath,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!recovery.IsSuccess)
        {
            return Failure(
                recovery.FailureKind,
                recovery.ErrorCode);
        }

        SafeChangeWorkspaceValidationResult validation =
            await validator.ValidateAsync(
                    request,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!validation.IsSuccess ||
            validation.Targets is null)
        {
            return Failure(
                validation.FailureKind,
                validation.ErrorCode);
        }

        var staged =
            new List<StagedChange>();

        var committed =
            new List<CommittedChange>();

        var createdDirectories =
            new List<string>();

        bool transactionCreated = false;

        try
        {
            if (!TryCreateTransactionDirectory(
                    request.Repository.WorkspacePath,
                    transactionPath))
            {
                return Failure(
                    SafeChangeApplicationFailureKind
                        .DestinationConflict,
                    "SAFE_CHANGE_TRANSACTION_EXISTS_OR_UNSAFE");
            }

            transactionCreated = true;

            for (
                int index = 0;
                index < validation.Targets.Count;
                index++)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                SafeChangeTarget target =
                    validation.Targets[index];

                byte[] content =
                    StrictUtf8.GetBytes(
                        target.Change.Content);

                string stagePath =
                    Path.Combine(
                        transactionPath,
                        $"stage-{index:D4}.tmp");

                await WriteStageAsync(
                        stagePath,
                        content,
                        cancellationToken)
                    .ConfigureAwait(false);

                string newHash =
                    Convert.ToHexString(
                            SHA256.HashData(
                                content))
                        .ToLowerInvariant();

                staged.Add(
                    new StagedChange(
                        index,
                        target,
                        stagePath,
                        Path.Combine(
                            transactionPath,
                            $"backup-{index:D4}.bin"),
                        newHash,
                        content.LongLength));
            }

            SafeChangeJournalResult journal =
                await SafeChangeTransactionJournal.WriteAsync(
                        request,
                        transactionPath,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!journal.IsSuccess)
            {
                return Failure(
                    journal.FailureKind,
                    journal.ErrorCode);
            }

            SafeChangeWorkspaceValidationResult
                finalValidation =
                    await validator.ValidateAsync(
                            request,
                            cancellationToken)
                        .ConfigureAwait(false);

            if (!finalValidation.IsSuccess ||
                finalValidation.Targets is null ||
                !TargetsMatch(
                    staged,
                    finalValidation.Targets))
            {
                return Failure(
                    SafeChangeApplicationFailureKind
                        .PreconditionFailed,
                    "SAFE_CHANGE_REVALIDATION_FAILED");
            }

            foreach (
                StagedChange change
                in staged)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                EnsureSafeParentDirectories(
                    request.Repository.RepositoryPath,
                    change.Target.DestinationPath,
                    createdDirectories);

                await CommitAsync(
                        change,
                        cancellationToken)
                    .ConfigureAwait(false);

                committed.Add(
                    new CommittedChange(
                        change));

                observer.AfterCommit(
                    change.Index);

                string publishedHash =
                    await HashSafeRegularFileAsync(
                            change.Target.DestinationPath,
                            cancellationToken)
                        .ConfigureAwait(false);

                if (!FixedTimeHashEquals(
                        change.NewSha256,
                        publishedHash))
                {
                    throw new SafeChangeCommitException(
                        SafeChangeApplicationFailureKind
                            .AtomicityFailure,
                        "SAFE_CHANGE_PUBLISHED_HASH_MISMATCH");
                }
            }

            var applied =
                committed
                    .Select(
                        item =>
                            new AppliedFileChange(
                                item.Change.Target
                                    .Change.Operation,
                                item.Change.Target
                                    .Change.RelativePath,
                                item.Change.Target
                                    .ExistingSha256,
                                item.Change.NewSha256,
                                item.Change.SizeBytes))
                    .ToArray();

            SafeChangeCompletionReceiptResult receipt =
                await SafeChangeCompletionReceipt.WriteAsync(
                        request,
                        applied,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!receipt.IsSuccess ||
                !receipt.IsCompleted)
            {
                bool rolledBack =
                    await RollbackAsync(
                            committed,
                            createdDirectories)
                        .ConfigureAwait(false);

                transactionCreated = false;

                return rolledBack
                    ? Failure(
                        receipt.FailureKind,
                        receipt.ErrorCode)
                    : Failure(
                        SafeChangeApplicationFailureKind.AtomicityFailure,
                        "SAFE_CHANGE_ROLLBACK_FAILED");
            }

            if (!TryDeleteTransaction(
                    transactionPath))
            {
                transactionCreated = false;

                return Failure(
                    SafeChangeApplicationFailureKind
                        .AtomicityFailure,
                    "SAFE_CHANGE_TRANSACTION_CLEANUP_FAILED");
            }

            transactionCreated = false;

            return SafeChangeApplicationResult.Success(
                new SafeChangeApplicationReport(
                    request.JobId,
                    request.RunId,
                    Array.AsReadOnly(
                        applied),
                    applied.Sum(
                        item =>
                            item.SizeBytes),
                    startedOnUtc,
                    DateTime.UtcNow));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            bool rolledBack =
                await RollbackAsync(
                        committed,
                        createdDirectories)
                    .ConfigureAwait(false);

            return rolledBack
                ? Failure(
                    SafeChangeApplicationFailureKind.Cancelled,
                    "SAFE_CHANGE_CANCELLED")
                : Failure(
                    SafeChangeApplicationFailureKind.AtomicityFailure,
                    "SAFE_CHANGE_ROLLBACK_FAILED");
        }
        catch (SafeChangeCommitException exception)
        {
            bool rolledBack =
                await RollbackAsync(
                        committed,
                        createdDirectories)
                    .ConfigureAwait(false);

            return rolledBack
                ? Failure(
                    exception.FailureKind,
                    exception.ErrorCode)
                : Failure(
                    SafeChangeApplicationFailureKind.AtomicityFailure,
                    "SAFE_CHANGE_ROLLBACK_FAILED");
        }
        catch (UnauthorizedAccessException)
        {
            bool rolledBack =
                await RollbackAsync(
                        committed,
                        createdDirectories)
                    .ConfigureAwait(false);

            return rolledBack
                ? Failure(
                    SafeChangeApplicationFailureKind.IoFailure,
                    "SAFE_CHANGE_ACCESS_DENIED")
                : Failure(
                    SafeChangeApplicationFailureKind.AtomicityFailure,
                    "SAFE_CHANGE_ROLLBACK_FAILED");
        }
        catch (IOException)
        {
            bool rolledBack =
                await RollbackAsync(
                        committed,
                        createdDirectories)
                    .ConfigureAwait(false);

            return rolledBack
                ? Failure(
                    SafeChangeApplicationFailureKind.IoFailure,
                    "SAFE_CHANGE_IO_FAILURE")
                : Failure(
                    SafeChangeApplicationFailureKind.AtomicityFailure,
                    "SAFE_CHANGE_ROLLBACK_FAILED");
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                  NotSupportedException or
                  PathTooLongException or
                  EncoderFallbackException)
        {
            bool rolledBack =
                await RollbackAsync(
                        committed,
                        createdDirectories)
                    .ConfigureAwait(false);

            return rolledBack
                ? Failure(
                    SafeChangeApplicationFailureKind.InvalidPath,
                    "SAFE_CHANGE_PATH_INVALID")
                : Failure(
                    SafeChangeApplicationFailureKind.AtomicityFailure,
                    "SAFE_CHANGE_ROLLBACK_FAILED");
        }
        finally
        {
            if (transactionCreated)
            {
                TryDeleteTransaction(
                    transactionPath);
            }
        }
    }

    private static async Task CommitAsync(
        StagedChange change,
        CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        string destination =
            change.Target.DestinationPath;

        if (change.Target.Change.Operation ==
            DeveloperChangeOperationType.CreateFile)
        {
            if (PathEntryExists(
                    destination))
            {
                throw new SafeChangeCommitException(
                    SafeChangeApplicationFailureKind
                        .DestinationConflict,
                    "SAFE_CHANGE_CREATE_DESTINATION_CHANGED");
            }

            File.Move(
                change.StagePath,
                destination,
                overwrite: false);

            return;
        }

        if (!IsSafeRegularFile(
                destination))
        {
            throw new SafeChangeCommitException(
                SafeChangeApplicationFailureKind
                    .PreconditionFailed,
                "SAFE_CHANGE_REPLACE_SOURCE_CHANGED");
        }

        string currentHash =
            await HashSafeRegularFileAsync(
                    destination,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!FixedTimeHashEquals(
                change.Target.Change
                    .ExpectedContentSha256,
                currentHash))
        {
            throw new SafeChangeCommitException(
                SafeChangeApplicationFailureKind
                    .PreconditionFailed,
                "SAFE_CHANGE_REPLACE_HASH_CHANGED");
        }

        if (PathEntryExists(
                change.BackupPath))
        {
            throw new SafeChangeCommitException(
                SafeChangeApplicationFailureKind
                    .DestinationConflict,
                "SAFE_CHANGE_BACKUP_EXISTS");
        }

        File.Replace(
            change.StagePath,
            destination,
            change.BackupPath,
            ignoreMetadataErrors: true);
    }

    private static async Task<bool> RollbackAsync(
        IReadOnlyList<CommittedChange> committed,
        IReadOnlyList<string> createdDirectories)
    {
        bool success =
            true;

        for (
            int index = committed.Count - 1;
            index >= 0;
            index--)
        {
            StagedChange change =
                committed[index].Change;

            try
            {
                if (change.Target.Change.Operation ==
                    DeveloperChangeOperationType.CreateFile)
                {
                    if (File.Exists(
                            change.Target.DestinationPath))
                    {
                        string currentHash =
                            await HashSafeRegularFileAsync(
                                    change.Target.DestinationPath,
                                    CancellationToken.None)
                                .ConfigureAwait(false);

                        if (!FixedTimeHashEquals(
                                change.NewSha256,
                                currentHash))
                        {
                            success = false;
                            continue;
                        }

                        File.Delete(
                            change.Target.DestinationPath);
                    }

                    continue;
                }

                if (!IsSafeRegularFile(
                        change.Target.DestinationPath) ||
                    !IsSafeRegularFile(
                        change.BackupPath))
                {
                    success = false;
                    continue;
                }

                string currentPublishedHash =
                    await HashSafeRegularFileAsync(
                            change.Target.DestinationPath,
                            CancellationToken.None)
                        .ConfigureAwait(false);

                if (!FixedTimeHashEquals(
                        change.NewSha256,
                        currentPublishedHash))
                {
                    success = false;
                    continue;
                }

                File.Replace(
                    change.BackupPath,
                    change.Target.DestinationPath,
                    destinationBackupFileName: null,
                    ignoreMetadataErrors: true);
            }
            catch
            {
                success = false;
            }
        }

        for (
            int index = createdDirectories.Count - 1;
            index >= 0;
            index--)
        {
            try
            {
                string directory =
                    createdDirectories[index];

                if (Directory.Exists(
                        directory) &&
                    !Directory.EnumerateFileSystemEntries(
                            directory)
                        .Any() &&
                    !IsLink(
                        new DirectoryInfo(
                            directory)))
                {
                    Directory.Delete(
                        directory);
                }
            }
            catch
            {
                success = false;
            }
        }

        return success;
    }

    private static void EnsureSafeParentDirectories(
        string repositoryRoot,
        string destination,
        ICollection<string> createdDirectories)
    {
        string? parent =
            Path.GetDirectoryName(
                destination);

        if (parent is null)
        {
            throw new SafeChangeCommitException(
                SafeChangeApplicationFailureKind.InvalidPath,
                "SAFE_CHANGE_PARENT_PATH_INVALID");
        }

        string relative =
            Path.GetRelativePath(
                repositoryRoot,
                parent);

        if (relative == ".")
        {
            return;
        }

        string current =
            repositoryRoot;

        foreach (
            string segment
            in relative.Split(
                Path.DirectorySeparatorChar))
        {
            current =
                Path.Combine(
                    current,
                    segment);

            if (PathEntryIsLink(
                    current))
            {
                throw new SafeChangeCommitException(
                    SafeChangeApplicationFailureKind
                        .SymlinkDetected,
                    "SAFE_CHANGE_PARENT_SYMLINK_DETECTED");
            }

            if (File.Exists(
                    current))
            {
                throw new SafeChangeCommitException(
                    SafeChangeApplicationFailureKind.InvalidPath,
                    "SAFE_CHANGE_PARENT_PATH_INVALID");
            }

            if (!Directory.Exists(
                    current))
            {
                Directory.CreateDirectory(
                    current);

                if (IsLink(
                        new DirectoryInfo(
                            current)))
                {
                    throw new SafeChangeCommitException(
                        SafeChangeApplicationFailureKind
                            .SymlinkDetected,
                        "SAFE_CHANGE_PARENT_SYMLINK_DETECTED");
                }

                createdDirectories.Add(
                    current);
            }
        }
    }

    private static async Task WriteStageAsync(
        string path,
        byte[] content,
        CancellationToken cancellationToken)
    {
        await using var stream =
            new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16_384,
                FileOptions.Asynchronous |
                FileOptions.WriteThrough);

        await stream.WriteAsync(
                content,
                cancellationToken)
            .ConfigureAwait(false);

        stream.Flush(
            flushToDisk: true);
    }

    private static bool TargetsMatch(
        IReadOnlyList<StagedChange> staged,
        IReadOnlyList<SafeChangeTarget> targets)
    {
        if (staged.Count !=
            targets.Count)
        {
            return false;
        }

        for (
            int index = 0;
            index < staged.Count;
            index++)
        {
            if (!string.Equals(
                    staged[index].Target.DestinationPath,
                    targets[index].DestinationPath,
                    PathComparison) ||
                !string.Equals(
                    staged[index].Target.ExistingSha256,
                    targets[index].ExistingSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryCreateTransactionDirectory(
        string workspacePath,
        string transactionPath)
    {
        try
        {
            string kronxy =
                Path.Combine(
                    workspacePath,
                    ".kronxy");

            string transactions =
                Path.Combine(
                    kronxy,
                    "change-transactions");

            foreach (
                string directory
                in new[]
                {
                    kronxy,
                    transactions
                })
            {
                Directory.CreateDirectory(
                    directory);

                if (IsLink(
                        new DirectoryInfo(
                            directory)))
                {
                    return false;
                }
            }

            if (Directory.Exists(
                    transactionPath) ||
                File.Exists(
                    transactionPath) ||
                PathEntryIsLink(
                    transactionPath))
            {
                return false;
            }

            Directory.CreateDirectory(
                transactionPath);

            return !IsLink(
                new DirectoryInfo(
                    transactionPath));
        }
        catch
        {
            return false;
        }
    }

    private static string GetTransactionPath(
        SafeChangeApplicationRequest request) =>
        SafeChangeProposalLineage.GetTransactionPath(request);

    private static bool TryDeleteTransaction(
        string transactionPath)
    {
        try
        {
            if (!Directory.Exists(
                    transactionPath) ||
                !TreeContainsNoLinks(
                    transactionPath))
            {
                return false;
            }

            Directory.Delete(
                transactionPath,
                recursive: true);

            return !PathEntryExists(
                transactionPath);
        }
        catch
        {
            return false;
        }
    }

    private static bool TreeContainsNoLinks(
        string root)
    {
        try
        {
            var pending =
                new Stack<DirectoryInfo>();

            pending.Push(
                new DirectoryInfo(
                    root));

            while (pending.Count > 0)
            {
                DirectoryInfo directory =
                    pending.Pop();

                if (IsLink(
                        directory))
                {
                    return false;
                }

                foreach (
                    FileSystemInfo entry
                    in directory
                        .EnumerateFileSystemInfos())
                {
                    if (IsLink(
                            entry))
                    {
                        return false;
                    }

                    if (entry is DirectoryInfo child)
                    {
                        pending.Push(
                            child);
                    }
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool PathEntryExists(
        string path)
    {
        return File.Exists(path) ||
               Directory.Exists(path) ||
               PathEntryIsLink(path);
    }

    private static bool PathEntryIsLink(
        string path)
    {
        try
        {
            var file =
                new FileInfo(
                    path);

            if (file.LinkTarget is not null ||
                file.Exists &&
                (file.Attributes &
                 FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }

            var directory =
                new DirectoryInfo(
                    path);

            return directory.LinkTarget is not null ||
                   directory.Exists &&
                   (directory.Attributes &
                    FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return true;
        }
    }

    private static bool IsSafeRegularFile(
        string path)
    {
        try
        {
            var info =
                new FileInfo(
                    path);

            return info.Exists &&
                   !IsLink(
                       info) &&
                   (info.Attributes &
                    FileAttributes.Directory) == 0;
        }
        catch
        {
            return false;
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

    private static async Task<string>
        HashSafeRegularFileAsync(
            string path,
            CancellationToken cancellationToken)
    {
        if (!IsSafeRegularFile(
                path))
        {
            throw new IOException(
                "Unsafe file.");
        }

        await using var stream =
            new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16_384,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);

        return Convert.ToHexString(
                await SHA256.HashDataAsync(
                        stream,
                        cancellationToken)
                    .ConfigureAwait(false))
            .ToLowerInvariant();
    }

    private static bool FixedTimeHashEquals(
        string? left,
        string? right)
    {
        if (left is null ||
            right is null)
        {
            return left is null &&
                   right is null;
        }

        try
        {
            return CryptographicOperations
                .FixedTimeEquals(
                    Convert.FromHexString(
                        left),
                    Convert.FromHexString(
                        right));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static StringComparison
        PathComparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static SafeChangeApplicationResult Failure(
        SafeChangeApplicationFailureKind kind,
        string errorCode)
    {
        return SafeChangeApplicationResult.Failure(
            kind,
            errorCode);
    }

    private sealed record StagedChange(
        int Index,
        SafeChangeTarget Target,
        string StagePath,
        string BackupPath,
        string NewSha256,
        long SizeBytes);

    private sealed record CommittedChange(
        StagedChange Change);

    private sealed class SafeChangeCommitException :
        Exception
    {
        public SafeChangeCommitException(
            SafeChangeApplicationFailureKind failureKind,
            string errorCode)
            : base(errorCode)
        {
            FailureKind =
                failureKind;

            ErrorCode =
                errorCode;
        }

        public SafeChangeApplicationFailureKind FailureKind
        {
            get;
        }

        public string ErrorCode { get; }
    }
}
