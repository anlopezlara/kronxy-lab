using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kronxy.Application.Execution;

namespace Kronxy.Infrastructure.Execution;

internal sealed record SafeChangeCompletionReceiptResult(
    bool IsSuccess,
    bool IsCompleted,
    IReadOnlyList<AppliedFileChange>? Changes,
    SafeChangeApplicationFailureKind FailureKind,
    string ErrorCode)
{
    public static SafeChangeCompletionReceiptResult Missing() =>
        new(true, false, null,
            SafeChangeApplicationFailureKind.None, string.Empty);

    public static SafeChangeCompletionReceiptResult Completed(
        IReadOnlyList<AppliedFileChange> changes) =>
        new(true, true, changes,
            SafeChangeApplicationFailureKind.None, string.Empty);

    public static SafeChangeCompletionReceiptResult Failure(
        string errorCode) =>
        new(false, false, null,
            SafeChangeApplicationFailureKind.AtomicityFailure,
            errorCode);
}

internal static class SafeChangeCompletionReceipt
{
    private const int CurrentVersion = 2;
    private const int LegacyVersion = 1;
    private const int MaximumReceiptBytes = 1_048_576;
    private const string TemporaryPrefix = ".kronxy-receipt-";
    private const string TemporarySuffix = ".tmp";

    private static readonly UTF8Encoding StrictUtf8 =
        new(false, true);

    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = false,
            WriteIndented = true,
            MaxDepth = 16
        };

    public static string GetPath(
        SafeChangeApplicationRequest request) =>
        SafeChangeProposalLineage.GetCompletionReceiptPath(request);

    public static async Task<SafeChangeCompletionReceiptResult>
        InspectAsync(
            SafeChangeApplicationRequest request,
            CancellationToken cancellationToken = default)
    {
        try
        {
            if (request.Repository is null ||
                request.Proposal is null)
                return Failure("SAFE_CHANGE_RECEIPT_REQUEST_INVALID");

            string receiptPath = GetPath(request);
            bool legacyFallback = false;

            if (!PathEntryExists(receiptPath) &&
                SafeChangeProposalLineage.HasVersionedIdentity(request))
            {
                string legacyPath =
                    SafeChangeProposalLineage
                        .GetLegacyCompletionReceiptPath(request);
                if (PathEntryExists(legacyPath))
                {
                    receiptPath = legacyPath;
                    legacyFallback = true;
                }
            }

            if (!IsExpectedReceiptPath(request, receiptPath) ||
                HasLinkInExistingPath(
                    request.Repository.WorkspacePath,
                    receiptPath))
                return Failure("SAFE_CHANGE_RECEIPT_PATH_UNSAFE");

            if (!PathEntryExists(receiptPath))
                return SafeChangeCompletionReceiptResult.Missing();

            if (!IsSafeRegularFile(receiptPath))
                return Failure("SAFE_CHANGE_RECEIPT_FILE_UNSAFE");

            var info = new FileInfo(receiptPath);
            if (info.Length <= 0 ||
                info.Length > MaximumReceiptBytes)
                return Failure("SAFE_CHANGE_RECEIPT_SIZE_INVALID");

            byte[] bytes = await File.ReadAllBytesAsync(
                receiptPath, cancellationToken).ConfigureAwait(false);

            ReceiptDocument? document =
                JsonSerializer.Deserialize<ReceiptDocument>(
                    bytes, JsonOptions);

            if (!ValidateIdentity(request, document, legacyFallback))
                return Failure("SAFE_CHANGE_RECEIPT_IDENTITY_INVALID");

            string fingerprint = SafeChangeProposalIdentity.Fingerprint(
                request.Proposal,
                document!.Version);
            if (!FixedTimeHashEquals(
                    document.ProposalFingerprintSha256,
                    fingerprint))
            {
                if (legacyFallback)
                    return SafeChangeCompletionReceiptResult.Missing();
                return Failure("SAFE_CHANGE_RECEIPT_PROPOSAL_MISMATCH");
            }

            if (document.Targets is null ||
                document.Targets.Count != request.Proposal.Changes.Count)
                return Failure("SAFE_CHANGE_RECEIPT_TARGETS_INVALID");

            string repositoryRoot = Path.GetFullPath(
                request.Repository.RepositoryPath);
            var seen = new HashSet<string>(PathComparer);
            var applied = new List<AppliedFileChange>(
                document.Targets.Count);

            for (int index = 0;
                index < document.Targets.Count;
                index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ReceiptTarget target = document.Targets[index];
                ValidatedDeveloperChange change =
                    request.Proposal.Changes[index];

                if (target.Operation != change.Operation ||
                    !string.Equals(target.RelativePath,
                        change.RelativePath,
                        StringComparison.Ordinal) ||
                    target.SizeBytes < 0 ||
                    !IsSha256(target.FinalSha256) ||
                    !seen.Add(target.RelativePath))
                    return Failure("SAFE_CHANGE_RECEIPT_TARGET_INVALID");

                string? destination = GetSafeDestination(
                    repositoryRoot, target.RelativePath);

                if (destination is null ||
                    HasLinkInExistingPath(repositoryRoot, destination) ||
                    !IsSafeRegularFile(destination))
                    return Failure("SAFE_CHANGE_RECEIPT_TARGET_UNSAFE");

                var finalInfo = new FileInfo(destination);
                if (finalInfo.Length != target.SizeBytes)
                    return Failure("SAFE_CHANGE_RECEIPT_TARGET_SIZE_MISMATCH");

                string finalHash = await HashFileAsync(
                    destination, cancellationToken).ConfigureAwait(false);

                string proposedHash = HashBytes(
                    StrictUtf8.GetBytes(change.Content));

                if (!FixedTimeHashEquals(target.FinalSha256, finalHash) ||
                    !FixedTimeHashEquals(target.FinalSha256, proposedHash))
                    return Failure("SAFE_CHANGE_RECEIPT_TARGET_HASH_MISMATCH");

                applied.Add(new AppliedFileChange(
                    change.Operation,
                    change.RelativePath,
                    string.IsNullOrEmpty(change.ExpectedContentSha256)
                        ? null
                        : change.ExpectedContentSha256.ToLowerInvariant(),
                    finalHash,
                    finalInfo.Length));
            }

            return SafeChangeCompletionReceiptResult.Completed(
                Array.AsReadOnly(applied.ToArray()));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return new SafeChangeCompletionReceiptResult(
                false, false, null,
                SafeChangeApplicationFailureKind.Cancelled,
                "SAFE_CHANGE_RECEIPT_CANCELLED");
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException or
                  JsonException or
                  ArgumentException or
                  NotSupportedException or
                  PathTooLongException or
                  EncoderFallbackException or
                  FormatException)
        {
            return Failure("SAFE_CHANGE_RECEIPT_INVALID");
        }
    }

    public static async Task<SafeChangeCompletionReceiptResult>
        WriteAsync(
            SafeChangeApplicationRequest request,
            IReadOnlyList<AppliedFileChange> changes,
            CancellationToken cancellationToken = default)
    {
        string? temporaryPath = null;

        try
        {
            if (request.Repository is null ||
                request.Proposal is null)
                return Failure("SAFE_CHANGE_RECEIPT_REQUEST_INVALID");

            string receiptPath = GetPath(request);

            if (!IsExpectedReceiptPath(request, receiptPath) ||
                changes.Count != request.Proposal.Changes.Count)
                return Failure("SAFE_CHANGE_RECEIPT_REQUEST_INVALID");

            string directory = Path.GetDirectoryName(receiptPath)!;
            if (!EnsureReceiptDirectory(request, directory) ||
                PathEntryExists(receiptPath))
                return Failure("SAFE_CHANGE_RECEIPT_DESTINATION_UNSAFE");

            var targets = new List<ReceiptTarget>(changes.Count);
            for (int index = 0; index < changes.Count; index++)
            {
                AppliedFileChange applied = changes[index];
                ValidatedDeveloperChange proposed =
                    request.Proposal.Changes[index];

                if (applied.Operation != proposed.Operation ||
                    !string.Equals(applied.RelativePath,
                        proposed.RelativePath,
                        StringComparison.Ordinal) ||
                    !IsSha256(applied.AfterSha256) ||
                    applied.SizeBytes < 0)
                    return Failure("SAFE_CHANGE_RECEIPT_TARGET_INVALID");

                targets.Add(new ReceiptTarget(
                    applied.Operation,
                    applied.RelativePath,
                    applied.AfterSha256.ToLowerInvariant(),
                    applied.SizeBytes));
            }

            var document = new ReceiptDocument(
                CurrentVersion,
                request.JobId,
                request.RunId,
                request.AttemptCount,
                SafeChangeProposalLineage.GetLineageId(request),
                request.Repository.JobExternalId,
                request.Repository.Branch,
                request.Repository.Head.ToLowerInvariant(),
                WorkspaceIdentity(request),
                SafeChangeProposalIdentity.Fingerprint(
                    request.Proposal,
                    CurrentVersion),
                targets);

            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
                document, JsonOptions);
            if (bytes.Length == 0 || bytes.Length > MaximumReceiptBytes)
                return Failure("SAFE_CHANGE_RECEIPT_SIZE_INVALID");

            temporaryPath = Path.Combine(
                directory,
                TemporaryPrefix + Guid.NewGuid().ToString("N") +
                TemporarySuffix);
            if (PathEntryExists(temporaryPath))
                return Failure("SAFE_CHANGE_RECEIPT_TEMPORARY_UNSAFE");

            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                16_384,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken)
                    .ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            if (!IsSafeRegularFile(temporaryPath))
                return Failure("SAFE_CHANGE_RECEIPT_TEMPORARY_UNSAFE");

            File.Move(temporaryPath, receiptPath, overwrite: false);
            temporaryPath = null;

            SafeChangeCompletionReceiptResult inspection =
                await InspectAsync(request, CancellationToken.None)
                    .ConfigureAwait(false);

            return inspection.IsCompleted
                ? inspection
                : Failure("SAFE_CHANGE_RECEIPT_PUBLISH_INVALID");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return new SafeChangeCompletionReceiptResult(
                false, false, null,
                SafeChangeApplicationFailureKind.Cancelled,
                "SAFE_CHANGE_RECEIPT_CANCELLED");
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException or
                  JsonException or
                  ArgumentException or
                  NotSupportedException or
                  PathTooLongException or
                  EncoderFallbackException)
        {
            return Failure("SAFE_CHANGE_RECEIPT_WRITE_FAILED");
        }
        finally
        {
            if (temporaryPath is not null)
                TryDelete(temporaryPath);
        }
    }

    private static bool ValidateIdentity(
        SafeChangeApplicationRequest request,
        ReceiptDocument? document,
        bool legacyFallback) =>
        document is not null &&
        (document.Version == CurrentVersion ||
         legacyFallback && document.Version == LegacyVersion) &&
        document.JobId == request.JobId &&
        document.RunId == request.RunId &&
        (document.Version == LegacyVersion ||
         document.AttemptCount == request.AttemptCount &&
         string.Equals(
             document.ProposalLineageId,
             SafeChangeProposalLineage.GetLineageId(request),
             StringComparison.Ordinal)) &&
        string.Equals(document.JobExternalId,
            request.Repository.JobExternalId,
            StringComparison.Ordinal) &&
        string.Equals(document.Branch,
            request.Repository.Branch,
            StringComparison.Ordinal) &&
        string.Equals(document.Head,
            request.Repository.Head,
            StringComparison.OrdinalIgnoreCase) &&
        IsSha256(document.WorkspaceIdentitySha256) &&
        FixedTimeHashEquals(document.WorkspaceIdentitySha256,
            WorkspaceIdentity(request)) &&
        IsSha256(document.ProposalFingerprintSha256);

    private static string WorkspaceIdentity(
        SafeChangeApplicationRequest request)
    {
        string identity = string.Concat(
            Path.GetFullPath(request.Repository.WorkspacePath),
            "\0",
            Path.GetFullPath(request.Repository.RepositoryPath));
        return HashBytes(StrictUtf8.GetBytes(identity));
    }

    private static bool EnsureReceiptDirectory(
        SafeChangeApplicationRequest request,
        string expectedDirectory)
    {
        string kronxy = Path.Combine(
            request.Repository.WorkspacePath, ".kronxy");
        string completions = Path.Combine(
            kronxy, "change-completions");

        if (!string.Equals(Path.GetFullPath(completions),
                Path.GetFullPath(expectedDirectory), PathComparison))
            return false;

        foreach (string directory in new[] { kronxy, completions })
        {
            Directory.CreateDirectory(directory);
            if (!IsSafeDirectory(directory))
                return false;
        }
        return true;
    }

    private static bool IsExpectedReceiptPath(
        SafeChangeApplicationRequest request,
        string receiptPath)
    {
        try
        {
            string fullPath = Path.GetFullPath(receiptPath);
            string expected = Path.GetFullPath(GetPath(request));
            string legacy = Path.GetFullPath(
                SafeChangeProposalLineage
                    .GetLegacyCompletionReceiptPath(request));
            return string.Equals(expected, fullPath, PathComparison) ||
                string.Equals(legacy, fullPath, PathComparison);
        }
        catch
        {
            return false;
        }
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
                return null;

            string[] segments = relativePath.Split(
                '/', StringSplitOptions.None);
            if (segments.Length == 0 ||
                segments.Any(segment =>
                    string.IsNullOrWhiteSpace(segment) ||
                    segment is "." or ".."))
                return null;

            string root = Path.GetFullPath(repositoryRoot);
            string destination = Path.GetFullPath(
                Path.Combine(root, Path.Combine(segments)));
            string prefix = root.EndsWith(
                Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;
            return destination.StartsWith(prefix, PathComparison)
                ? destination
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool HasLinkInExistingPath(
        string root,
        string destination)
    {
        try
        {
            string current = Path.GetFullPath(destination);
            string normalizedRoot = Path.GetFullPath(root);
            while (!string.Equals(current,
                normalizedRoot, PathComparison))
            {
                if (PathEntryExists(current))
                {
                    FileSystemInfo info = Directory.Exists(current)
                        ? new DirectoryInfo(current)
                        : new FileInfo(current);
                    if (IsLink(info))
                        return true;
                }
                current = Path.GetDirectoryName(current)!;
                if (string.IsNullOrEmpty(current))
                    return true;
            }
            return IsLink(new DirectoryInfo(normalizedRoot));
        }
        catch
        {
            return true;
        }
    }

    private static async Task<string> HashFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            16_384,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(
            await SHA256.HashDataAsync(stream, cancellationToken)
                .ConfigureAwait(false)).ToLowerInvariant();
    }

    private static string HashBytes(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes))
            .ToLowerInvariant();

    private static bool IsSha256(string? value) =>
        value is not null &&
        value.Length == 64 &&
        value.All(character =>
            character is >= '0' and <= '9' or
            >= 'a' and <= 'f' or
            >= 'A' and <= 'F');

    private static bool FixedTimeHashEquals(
        string left,
        string right)
    {
        if (!IsSha256(left) || !IsSha256(right))
            return false;
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(left),
            Convert.FromHexString(right));
    }

    private static bool IsSafeRegularFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists && !IsLink(info) &&
                (info.Attributes & FileAttributes.Directory) == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSafeDirectory(string path)
    {
        try
        {
            var info = new DirectoryInfo(path);
            return info.Exists && !IsLink(info);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsLink(FileSystemInfo info) =>
        info.LinkTarget is not null ||
        (info.Attributes & FileAttributes.ReparsePoint) != 0;

    private static bool PathEntryExists(string path) =>
        File.Exists(path) || Directory.Exists(path) ||
        new FileInfo(path).LinkTarget is not null ||
        new DirectoryInfo(path).LinkTarget is not null;

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static SafeChangeCompletionReceiptResult Failure(
        string code) =>
        SafeChangeCompletionReceiptResult.Failure(code);

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private sealed record ReceiptDocument(
        int Version,
        Guid JobId,
        Guid RunId,
        int AttemptCount,
        string? ProposalLineageId,
        string JobExternalId,
        string Branch,
        string Head,
        string WorkspaceIdentitySha256,
        string ProposalFingerprintSha256,
        IReadOnlyList<ReceiptTarget> Targets);

    private sealed record ReceiptTarget(
        DeveloperChangeOperationType Operation,
        string RelativePath,
        string FinalSha256,
        long SizeBytes);
}
