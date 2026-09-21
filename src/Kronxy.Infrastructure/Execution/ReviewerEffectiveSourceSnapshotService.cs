using System.Security.Cryptography;
using System.Text;
using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;

namespace Kronxy.Infrastructure.Execution;

public sealed class ReviewerEffectiveSourceSnapshotService :
    IReviewerEffectiveSourceSnapshotService
{
    private const long MaxFileBytes = 1024 * 1024;
    private const long MaxTotalBytes = 4 * 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public async Task<ReviewerEffectiveSourceSnapshotResult> CaptureAsync(
        ReviewerEffectiveSourceSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ValidRequest(request))
            return Failure(ReviewerEffectiveSourceFailureKind.InvalidRequest,
                "REVIEWER_EFFECTIVE_SOURCE_INVALID_REQUEST");

        try
        {
            string root = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(request.Repository.RepositoryPath));
            string rootPrefix = root + Path.DirectorySeparatorChar;
            if (!Directory.Exists(root) || HasReparsePoint(root))
                return Failure(ReviewerEffectiveSourceFailureKind.UnsafePath,
                    "REVIEWER_EFFECTIVE_SOURCE_UNSAFE_ROOT");

            Dictionary<string, ObservedChangeManifestEntry> observed =
                request.ObservedChanges.Entries.ToDictionary(
                    entry => Normalize(entry.RelativePath),
                    StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ValidatedDeveloperChange> proposed =
                request.EffectiveProposal.Changes.ToDictionary(
                    change => Normalize(change.RelativePath),
                    StringComparer.OrdinalIgnoreCase);

            var files = new List<ReviewerEffectiveSourceFile>();
            long totalBytes = 0;
            foreach (string rawPath in request.Plan.CandidateFilesToModify)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relativePath = Normalize(rawPath);
                if (!IsSafeRelativePath(relativePath))
                    return Failure(ReviewerEffectiveSourceFailureKind.UnsafePath,
                        "REVIEWER_EFFECTIVE_SOURCE_PATH_INVALID");

                string fullPath = Path.GetFullPath(
                    Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!fullPath.StartsWith(rootPrefix, PathComparison) ||
                    HasReparsePointBetween(root, fullPath))
                    return Failure(ReviewerEffectiveSourceFailureKind.UnsafePath,
                        "REVIEWER_EFFECTIVE_SOURCE_PATH_OUTSIDE_WORKSPACE");
                if (!File.Exists(fullPath))
                    return Failure(ReviewerEffectiveSourceFailureKind.MissingSource,
                        "REVIEWER_EFFECTIVE_SOURCE_MISSING");

                FileInfo info = new(fullPath);
                if (info.Length > MaxFileBytes || totalBytes + info.Length > MaxTotalBytes)
                    return Failure(ReviewerEffectiveSourceFailureKind.SourceTooLarge,
                        "REVIEWER_EFFECTIVE_SOURCE_LIMIT_EXCEEDED");

                byte[] bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken)
                    .ConfigureAwait(false);
                string sha256 = Convert.ToHexString(SHA256.HashData(bytes))
                    .ToLowerInvariant();
                string content = StrictUtf8.GetString(bytes);
                totalBytes += bytes.LongLength;

                if (observed.TryGetValue(relativePath, out ObservedChangeManifestEntry? entry) &&
                    (!entry.FinalSizeBytes.HasValue || entry.FinalSizeBytes.Value != bytes.LongLength ||
                     !string.Equals(entry.FinalSha256, sha256, StringComparison.OrdinalIgnoreCase)))
                    return Failure(ReviewerEffectiveSourceFailureKind.StaleSource,
                        "REVIEWER_EFFECTIVE_SOURCE_MANIFEST_MISMATCH");

                if (proposed.TryGetValue(relativePath, out ValidatedDeveloperChange? change) &&
                    !string.Equals(
                        Convert.ToHexString(SHA256.HashData(StrictUtf8.GetBytes(change.Content)))
                            .ToLowerInvariant(),
                        sha256,
                        StringComparison.OrdinalIgnoreCase))
                    return Failure(ReviewerEffectiveSourceFailureKind.StaleSource,
                        "REVIEWER_EFFECTIVE_SOURCE_PROPOSAL_MISMATCH");

                files.Add(new ReviewerEffectiveSourceFile(
                    relativePath, sha256, bytes.LongLength, content));
            }

            return ReviewerEffectiveSourceSnapshotResult.Success(
                new ReviewerEffectiveSourceSnapshot(
                    request.JobId,
                    request.RunId,
                    request.EffectiveProposalLineage,
                    files));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure(ReviewerEffectiveSourceFailureKind.Cancelled,
                "REVIEWER_EFFECTIVE_SOURCE_CANCELLED");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            return Failure(ReviewerEffectiveSourceFailureKind.IoFailure,
                "REVIEWER_EFFECTIVE_SOURCE_READ_FAILED");
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Failure(ReviewerEffectiveSourceFailureKind.UnsafePath,
                "REVIEWER_EFFECTIVE_SOURCE_PATH_INVALID");
        }
    }

    private static bool ValidRequest(ReviewerEffectiveSourceSnapshotRequest request)
    {
        if (request is null || request.JobId == Guid.Empty || request.RunId == Guid.Empty ||
            request.Repository is null || request.Plan is null ||
            request.EffectiveProposal is null || request.ObservedChanges is null ||
            request.Repository.JobId != request.JobId ||
            request.ObservedChanges.JobId != request.JobId ||
            request.ObservedChanges.RunId != request.RunId ||
            !Enum.IsDefined(request.EffectiveProposalLineage) ||
            request.Plan.CandidateFilesToModify is not { Count: > 0 and <= 100 })
            return false;

        string[] allowlist = request.Plan.CandidateFilesToModify
            .Select(Normalize).ToArray();
        if (allowlist.Distinct(StringComparer.OrdinalIgnoreCase).Count() != allowlist.Length ||
            allowlist.Any(path => !IsSafeRelativePath(path)))
            return false;

        HashSet<string> allowed = allowlist.ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] proposed = request.EffectiveProposal.Changes
            .Select(change => Normalize(change.RelativePath)).ToArray();
        string[] observed = request.ObservedChanges.Entries
            .Select(entry => Normalize(entry.RelativePath)).ToArray();
        return proposed.Distinct(StringComparer.OrdinalIgnoreCase).Count() == proposed.Length &&
            observed.Distinct(StringComparer.OrdinalIgnoreCase).Count() == observed.Length &&
            proposed.All(allowed.Contains) && observed.All(allowed.Contains) &&
            proposed.ToHashSet(StringComparer.OrdinalIgnoreCase)
                .IsSubsetOf(observed);
    }

    private static string Normalize(string path) =>
        (path ?? string.Empty).Replace('\\', '/').Trim();

    private static bool IsSafeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.IndexOf('\0') >= 0 ||
            Path.IsPathFullyQualified(path) || path.StartsWith('/') || path.StartsWith('\\'))
            return false;
        string[] segments = path.Split('/');
        return segments.All(segment =>
            !string.IsNullOrWhiteSpace(segment) && segment is not "." and not "..");
    }

    private static bool HasReparsePointBetween(string root, string fullPath)
    {
        string relative = Path.GetRelativePath(root, fullPath);
        string current = root;
        foreach (string segment in relative.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if ((File.Exists(current) || Directory.Exists(current)) && HasReparsePoint(current))
                return true;
        }
        return false;
    }

    private static bool HasReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static ReviewerEffectiveSourceSnapshotResult Failure(
        ReviewerEffectiveSourceFailureKind kind,
        string code) =>
        ReviewerEffectiveSourceSnapshotResult.Failure(kind, code);
}
