using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kronxy.Application.Execution;

namespace Kronxy.Infrastructure.Execution;

internal sealed record SafeChangeJournalResult(
    bool IsSuccess,
    bool WasRecovered,
    SafeChangeApplicationFailureKind FailureKind,
    string ErrorCode)
{
    public static SafeChangeJournalResult Success(
        bool wasRecovered = false)
    {
        return new SafeChangeJournalResult(
            true,
            wasRecovered,
            SafeChangeApplicationFailureKind.None,
            string.Empty);
    }

    public static SafeChangeJournalResult Failure(
        SafeChangeApplicationFailureKind failureKind,
        string errorCode)
    {
        return new SafeChangeJournalResult(
            false,
            false,
            failureKind,
            errorCode);
    }
}

internal static class SafeChangeTransactionJournal
{
    private const int CurrentVersion = 1;
    private const int MaximumJournalBytes = 1_048_576;
    private const string ManifestName = "manifest.json";
    private const string TemporaryManifestName =
        "manifest.json.tmp";

    private static readonly UTF8Encoding StrictUtf8 =
        new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = false,
            WriteIndented = true,
            MaxDepth = 16
        };

    public static async Task<SafeChangeJournalResult>
        WriteAsync(
            SafeChangeApplicationRequest request,
            string transactionPath,
            CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsExpectedTransactionPath(
                    request,
                    transactionPath) ||
                !IsSafeDirectoryTree(
                    transactionPath))
            {
                return Failure(
                    "SAFE_CHANGE_JOURNAL_PATH_UNSAFE");
            }

            var entries =
                new List<JournalEntry>(
                    request.Proposal.Changes.Count);

            for (
                int index = 0;
                index < request.Proposal.Changes.Count;
                index++)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                ValidatedDeveloperChange change =
                    request.Proposal.Changes[index];

                string? destination =
                    GetSafeDestination(
                        request.Repository.RepositoryPath,
                        change.RelativePath);

                if (destination is null)
                {
                    return Failure(
                        "SAFE_CHANGE_JOURNAL_DESTINATION_UNSAFE");
                }

                byte[] content =
                    StrictUtf8.GetBytes(
                        change.Content);

                string afterSha256 =
                    HashBytes(
                        content);

                string stagePath =
                    Path.Combine(
                        transactionPath,
                        $"stage-{index:D4}.tmp");

                if (!IsSafeRegularFile(
                        stagePath) ||
                    !FixedTimeHashEquals(
                        afterSha256,
                        await HashFileAsync(
                                stagePath,
                                cancellationToken)
                            .ConfigureAwait(false)))
                {
                    return Failure(
                        "SAFE_CHANGE_JOURNAL_STAGE_INVALID");
                }

                string? beforeSha256 =
                    string.IsNullOrEmpty(
                        change.ExpectedContentSha256)
                        ? null
                        : change.ExpectedContentSha256
                            .ToLowerInvariant();

                entries.Add(
                    new JournalEntry(
                        index,
                        change.Operation,
                        change.RelativePath,
                        beforeSha256,
                        afterSha256));
            }

            var document =
                new JournalDocument(
                    CurrentVersion,
                    request.JobId,
                    request.RunId,
                    Path.GetFullPath(
                        request.Repository.RepositoryPath),
                    entries);

            byte[] bytes =
                JsonSerializer.SerializeToUtf8Bytes(
                    document,
                    JsonOptions);

            if (bytes.Length == 0 ||
                bytes.Length > MaximumJournalBytes)
            {
                return Failure(
                    "SAFE_CHANGE_JOURNAL_SIZE_INVALID");
            }

            string manifestPath =
                Path.Combine(
                    transactionPath,
                    ManifestName);

            string temporaryPath =
                Path.Combine(
                    transactionPath,
                    TemporaryManifestName);

            if (PathEntryExists(manifestPath) ||
                PathEntryExists(temporaryPath))
            {
                return Failure(
                    "SAFE_CHANGE_JOURNAL_ALREADY_EXISTS");
            }

            await WriteDurableAsync(
                    temporaryPath,
                    bytes,
                    cancellationToken)
                .ConfigureAwait(false);

            File.Move(
                temporaryPath,
                manifestPath,
                overwrite: false);

            if (!IsSafeRegularFile(
                    manifestPath))
            {
                return Failure(
                    "SAFE_CHANGE_JOURNAL_PUBLISH_FAILED");
            }

            return SafeChangeJournalResult.Success();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return SafeChangeJournalResult.Failure(
                SafeChangeApplicationFailureKind.Cancelled,
                "SAFE_CHANGE_JOURNAL_CANCELLED");
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException or
                  JsonException or
                  ArgumentException or
                  NotSupportedException or
                  EncoderFallbackException)
        {
            return Failure(
                "SAFE_CHANGE_JOURNAL_WRITE_FAILED");
        }
    }

    public static async Task<SafeChangeJournalResult>
        RecoverAsync(
            SafeChangeApplicationRequest request,
            string transactionPath,
            CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsExpectedTransactionPath(
                    request,
                    transactionPath))
            {
                return Failure(
                    "SAFE_CHANGE_RECOVERY_PATH_UNSAFE");
            }

            if (!PathEntryExists(
                    transactionPath))
            {
                return SafeChangeJournalResult.Success();
            }

            if (!IsSafeDirectoryTree(
                    transactionPath))
            {
                return Failure(
                    "SAFE_CHANGE_RECOVERY_TREE_UNSAFE");
            }

            string manifestPath =
                Path.Combine(
                    transactionPath,
                    ManifestName);

            if (!IsSafeRegularFile(
                    manifestPath))
            {
                return Failure(
                    "SAFE_CHANGE_RECOVERY_MANIFEST_MISSING");
            }

            var manifestInfo =
                new FileInfo(
                    manifestPath);

            if (manifestInfo.Length <= 0 ||
                manifestInfo.Length > MaximumJournalBytes)
            {
                return Failure(
                    "SAFE_CHANGE_RECOVERY_MANIFEST_SIZE_INVALID");
            }

            byte[] bytes =
                await File.ReadAllBytesAsync(
                        manifestPath,
                        cancellationToken)
                    .ConfigureAwait(false);

            JournalDocument? document =
                JsonSerializer.Deserialize<JournalDocument>(
                    bytes,
                    JsonOptions);

            if (!ValidateDocument(
                    request,
                    document))
            {
                return Failure(
                    "SAFE_CHANGE_RECOVERY_MANIFEST_INVALID");
            }

            string repositoryRoot =
                Path.GetFullPath(
                    request.Repository.RepositoryPath);

            for (
                int index = document!.Entries.Count - 1;
                index >= 0;
                index--)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                JournalEntry entry =
                    document.Entries[index];

                string? destination =
                    GetSafeDestination(
                        repositoryRoot,
                        entry.RelativePath);

                if (destination is null ||
                    HasLinkInExistingPath(
                        repositoryRoot,
                        destination))
                {
                    return Failure(
                        "SAFE_CHANGE_RECOVERY_DESTINATION_UNSAFE");
                }

                string backupPath =
                    Path.Combine(
                        transactionPath,
                        $"backup-{entry.Index:D4}.bin");

                if (entry.Operation ==
                    DeveloperChangeOperationType.CreateFile)
                {
                    SafeChangeJournalResult createRecovery =
                        await RecoverCreateAsync(
                                repositoryRoot,
                                destination,
                                entry,
                                cancellationToken)
                            .ConfigureAwait(false);

                    if (!createRecovery.IsSuccess)
                    {
                        return createRecovery;
                    }

                    continue;
                }

                SafeChangeJournalResult replaceRecovery =
                    await RecoverReplaceAsync(
                            destination,
                            backupPath,
                            entry,
                            cancellationToken)
                        .ConfigureAwait(false);

                if (!replaceRecovery.IsSuccess)
                {
                    return replaceRecovery;
                }
            }

            if (!TryDeleteTransaction(
                    transactionPath))
            {
                return Failure(
                    "SAFE_CHANGE_RECOVERY_CLEANUP_FAILED");
            }

            return SafeChangeJournalResult.Success(
                wasRecovered: true);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return SafeChangeJournalResult.Failure(
                SafeChangeApplicationFailureKind.Cancelled,
                "SAFE_CHANGE_RECOVERY_CANCELLED");
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException or
                  JsonException or
                  ArgumentException or
                  NotSupportedException or
                  FormatException)
        {
            return Failure(
                "SAFE_CHANGE_RECOVERY_FAILED");
        }
    }

    private static async Task<SafeChangeJournalResult>
        RecoverCreateAsync(
            string repositoryRoot,
            string destination,
            JournalEntry entry,
            CancellationToken cancellationToken)
    {
        if (!PathEntryExists(
                destination))
        {
            RemoveEmptyParents(
                repositoryRoot,
                destination);

            return SafeChangeJournalResult.Success(
                wasRecovered: true);
        }

        if (!IsSafeRegularFile(
                destination))
        {
            return Failure(
                "SAFE_CHANGE_RECOVERY_CREATE_CONFLICT");
        }

        string currentHash =
            await HashFileAsync(
                    destination,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!FixedTimeHashEquals(
                entry.AfterSha256,
                currentHash))
        {
            return Failure(
                "SAFE_CHANGE_RECOVERY_CREATE_HASH_CONFLICT");
        }

        File.Delete(
            destination);

        RemoveEmptyParents(
            repositoryRoot,
            destination);

        return SafeChangeJournalResult.Success(
            wasRecovered: true);
    }

    private static async Task<SafeChangeJournalResult>
        RecoverReplaceAsync(
            string destination,
            string backupPath,
            JournalEntry entry,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                entry.BeforeSha256))
        {
            return Failure(
                "SAFE_CHANGE_RECOVERY_BEFORE_HASH_MISSING");
        }

        if (PathEntryExists(
                backupPath))
        {
            if (!IsSafeRegularFile(destination) ||
                !IsSafeRegularFile(backupPath))
            {
                return Failure(
                    "SAFE_CHANGE_RECOVERY_REPLACE_UNSAFE");
            }

            string destinationHash =
                await HashFileAsync(
                        destination,
                        cancellationToken)
                    .ConfigureAwait(false);

            string backupHash =
                await HashFileAsync(
                        backupPath,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!FixedTimeHashEquals(
                    entry.AfterSha256,
                    destinationHash) ||
                !FixedTimeHashEquals(
                    entry.BeforeSha256,
                    backupHash))
            {
                return Failure(
                    "SAFE_CHANGE_RECOVERY_REPLACE_HASH_CONFLICT");
            }

            File.Replace(
                backupPath,
                destination,
                destinationBackupFileName: null,
                ignoreMetadataErrors: true);

            string restoredHash =
                await HashFileAsync(
                        destination,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!FixedTimeHashEquals(
                    entry.BeforeSha256,
                    restoredHash))
            {
                return Failure(
                    "SAFE_CHANGE_RECOVERY_RESTORE_MISMATCH");
            }

            return SafeChangeJournalResult.Success(
                wasRecovered: true);
        }

        if (!IsSafeRegularFile(
                destination))
        {
            return Failure(
                "SAFE_CHANGE_RECOVERY_REPLACE_SOURCE_MISSING");
        }

        string currentHash =
            await HashFileAsync(
                    destination,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!FixedTimeHashEquals(
                entry.BeforeSha256,
                currentHash))
        {
            return Failure(
                "SAFE_CHANGE_RECOVERY_REPLACE_AMBIGUOUS");
        }

        return SafeChangeJournalResult.Success(
            wasRecovered: true);
    }

    private static bool ValidateDocument(
        SafeChangeApplicationRequest request,
        JournalDocument? document)
    {
        if (document is null ||
            document.Version != CurrentVersion ||
            document.JobId != request.JobId ||
            document.RunId != request.RunId ||
            document.Entries is null ||
            document.Entries.Count !=
                request.Proposal.Changes.Count ||
            !string.Equals(
                document.RepositoryPath,
                Path.GetFullPath(
                    request.Repository.RepositoryPath),
                PathComparison))
        {
            return false;
        }

        for (
            int index = 0;
            index < document.Entries.Count;
            index++)
        {
            JournalEntry entry =
                document.Entries[index];

            ValidatedDeveloperChange change =
                request.Proposal.Changes[index];

            string? expectedBefore =
                string.IsNullOrEmpty(
                    change.ExpectedContentSha256)
                    ? null
                    : change.ExpectedContentSha256
                        .ToLowerInvariant();

            string expectedAfter;

            try
            {
                expectedAfter =
                    HashBytes(
                        StrictUtf8.GetBytes(
                            change.Content));
            }
            catch (EncoderFallbackException)
            {
                return false;
            }

            if (entry.Index != index ||
                entry.Operation != change.Operation ||
                !string.Equals(
                    entry.RelativePath,
                    change.RelativePath,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    entry.BeforeSha256,
                    expectedBefore,
                    StringComparison.OrdinalIgnoreCase) ||
                !FixedTimeHashEquals(
                    entry.AfterSha256,
                    expectedAfter))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task WriteDurableAsync(
        string path,
        byte[] bytes,
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
                bytes,
                cancellationToken)
            .ConfigureAwait(false);

        stream.Flush(
            flushToDisk: true);
    }

    private static async Task<string> HashFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!IsSafeRegularFile(
                path))
        {
            throw new IOException(
                "Unsafe journal file.");
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

    private static string HashBytes(
        byte[] bytes)
    {
        return Convert.ToHexString(
                SHA256.HashData(
                    bytes))
            .ToLowerInvariant();
    }

    private static string? GetSafeDestination(
        string repositoryRoot,
        string relativePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(relativePath) ||
                Path.IsPathFullyQualified(relativePath) ||
                relativePath.Contains('\\') ||
                relativePath.Contains(':'))
            {
                return null;
            }

            string[] segments =
                relativePath.Split(
                    '/',
                    StringSplitOptions.None);

            if (segments.Length == 0 ||
                segments.Any(
                    segment =>
                        string.IsNullOrWhiteSpace(segment) ||
                        segment is "." or ".."))
            {
                return null;
            }

            string root =
                Path.GetFullPath(
                    repositoryRoot);

            string destination =
                Path.GetFullPath(
                    Path.Combine(
                        root,
                        Path.Combine(
                            segments)));

            string prefix =
                root.EndsWith(
                    Path.DirectorySeparatorChar)
                    ? root
                    : root +
                      Path.DirectorySeparatorChar;

            return destination.StartsWith(
                prefix,
                PathComparison)
                ? destination
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsExpectedTransactionPath(
        SafeChangeApplicationRequest request,
        string transactionPath)
    {
        try
        {
            string expected =
                Path.GetFullPath(
                    Path.Combine(
                        request.Repository.WorkspacePath,
                        ".kronxy",
                        "change-transactions",
                        $"{request.JobId:N}-{request.RunId:N}" +
                        (request.IsHumanReviewCorrection
                            ? "-human-review-correction"
                            : request.IsBuildCorrection
                                ? "-build-correction"
                                : string.Empty)));

            return string.Equals(
                expected,
                Path.GetFullPath(transactionPath),
                PathComparison);
        }
        catch
        {
            return false;
        }
    }

    private static bool HasLinkInExistingPath(
        string repositoryRoot,
        string destination)
    {
        try
        {
            string root =
                Path.GetFullPath(
                    repositoryRoot);

            string relative =
                Path.GetRelativePath(
                    root,
                    destination);

            string current =
                root;

            foreach (
                string segment in relative.Split(
                    Path.DirectorySeparatorChar,
                    StringSplitOptions.RemoveEmptyEntries))
            {
                current =
                    Path.Combine(
                        current,
                        segment);

                if (PathEntryIsLink(
                        current))
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

    private static void RemoveEmptyParents(
        string repositoryRoot,
        string destination)
    {
        string root =
            Path.GetFullPath(
                repositoryRoot);

        string? current =
            Path.GetDirectoryName(
                destination);

        while (current is not null &&
               !string.Equals(
                   current,
                   root,
                   PathComparison) &&
               current.StartsWith(
                   root + Path.DirectorySeparatorChar,
                   PathComparison))
        {
            if (!Directory.Exists(current) ||
                IsLink(new DirectoryInfo(current)) ||
                Directory.EnumerateFileSystemEntries(current)
                    .Any())
            {
                break;
            }

            Directory.Delete(current);

            current =
                Path.GetDirectoryName(
                    current);
        }
    }

    private static bool TryDeleteTransaction(
        string transactionPath)
    {
        try
        {
            if (!Directory.Exists(transactionPath) ||
                !IsSafeDirectoryTree(transactionPath))
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

    private static bool IsSafeDirectoryTree(
        string root)
    {
        try
        {
            var rootInfo =
                new DirectoryInfo(
                    root);

            if (!rootInfo.Exists ||
                IsLink(rootInfo))
            {
                return false;
            }

            var pending =
                new Stack<DirectoryInfo>();

            pending.Push(
                rootInfo);

            while (pending.Count > 0)
            {
                DirectoryInfo directory =
                    pending.Pop();

                if (IsLink(directory))
                {
                    return false;
                }

                foreach (
                    FileSystemInfo entry
                    in directory.EnumerateFileSystemInfos())
                {
                    if (IsLink(entry))
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

    private static bool IsSafeRegularFile(
        string path)
    {
        try
        {
            var info =
                new FileInfo(
                    path);

            return info.Exists &&
                   !IsLink(info) &&
                   (info.Attributes &
                    FileAttributes.Directory) == 0;
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
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(left),
                Convert.FromHexString(right));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static SafeChangeJournalResult Failure(
        string errorCode)
    {
        return SafeChangeJournalResult.Failure(
            SafeChangeApplicationFailureKind.AtomicityFailure,
            errorCode);
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private sealed record JournalDocument(
        int Version,
        Guid JobId,
        Guid RunId,
        string RepositoryPath,
        IReadOnlyList<JournalEntry> Entries);

    private sealed record JournalEntry(
        int Index,
        DeveloperChangeOperationType Operation,
        string RelativePath,
        string? BeforeSha256,
        string AfterSha256);
}
