using System.Security.Cryptography;
using System.Text;
using Kronxy.Application.Artifacts;

namespace Kronxy.Infrastructure.Artifacts;

public sealed class FileSystemArtifactStore : IArtifactStore
{
    private const string TemporaryPrefix = ".kronxy-artifact-";
    private const string TemporarySuffix = ".tmp";

    private readonly ArtifactStoreOptions options;
    private readonly string rootPath;

    public FileSystemArtifactStore(
        ArtifactStoreOptions options)
    {
        this.options =
            options ??
            throw new ArgumentNullException(nameof(options));

        this.options.Validate();

        rootPath =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(options.RootPath));
    }

    public async Task<ArtifactWriteResult> WriteAsync(
        ArtifactWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidRequest(request))
        {
            return Failure(
                ArtifactStoreFailureKind.InvalidRequest,
                "ARTIFACT_INVALID_REQUEST");
        }

        if (request.Content.Length > options.MaxArtifactBytes)
        {
            return Failure(
                ArtifactStoreFailureKind.ArtifactTooLarge,
                "ARTIFACT_TOO_LARGE");
        }

        string? temporaryPath = null;
        string? destinationPath = null;
        bool ownsTemporary = false;
        bool ownsPublished = false;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!EnsureSafeDirectory(rootPath))
            {
                return Failure(
                    ArtifactStoreFailureKind.UnsafeRoot,
                    "ARTIFACT_ROOT_UNSAFE");
            }

            var layout = GetLayout(
                request.ArtifactType,
                request.CorrelationId);

            string jobSegment =
                request.JobId.ToString("N");

            string runSegment =
                request.RunId.ToString("N");

            string jobPath =
                Path.Combine(rootPath, jobSegment);

            string runPath =
                Path.Combine(jobPath, runSegment);

            string typePath =
                Path.Combine(runPath, layout.Directory);

            if (!EnsureSafeChildDirectory(
                    rootPath,
                    jobPath) ||
                !EnsureSafeChildDirectory(
                    rootPath,
                    runPath) ||
                !EnsureSafeChildDirectory(
                    rootPath,
                    typePath))
            {
                return Failure(
                    ArtifactStoreFailureKind.UnsafePath,
                    "ARTIFACT_PATH_UNSAFE");
            }

            destinationPath =
                Path.Combine(
                    typePath,
                    layout.FileName);

            if (!IsDirectChild(
                    typePath,
                    destinationPath))
            {
                return Failure(
                    ArtifactStoreFailureKind.UnsafePath,
                    "ARTIFACT_DESTINATION_UNSAFE");
            }

            if (PathEntryIsLink(destinationPath))
            {
                return Failure(
                    ArtifactStoreFailureKind.UnsafePath,
                    "ARTIFACT_DESTINATION_IS_LINK");
            }

            if (File.Exists(destinationPath) ||
                Directory.Exists(destinationPath))
            {
                return Failure(
                    ArtifactStoreFailureKind.DestinationExists,
                    "ARTIFACT_DESTINATION_EXISTS");
            }

            temporaryPath =
                Path.Combine(
                    typePath,
                    TemporaryPrefix +
                    Guid.NewGuid().ToString("N") +
                    TemporarySuffix);

            if (!IsDirectChild(
                    typePath,
                    temporaryPath) ||
                PathEntryExists(temporaryPath))
            {
                return Failure(
                    ArtifactStoreFailureKind.UnsafePath,
                    "ARTIFACT_TEMPORARY_PATH_UNSAFE");
            }

            string expectedHash =
                Convert.ToHexString(
                    SHA256.HashData(
                        request.Content.Span))
                .ToLowerInvariant();

            await using (var stream =
                new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 16_384,
                    FileOptions.Asynchronous |
                    FileOptions.WriteThrough))
            {
                ownsTemporary = true;

                await stream.WriteAsync(
                        request.Content,
                        cancellationToken)
                    .ConfigureAwait(false);

                await stream.FlushAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!IsSafeRegularFile(temporaryPath))
            {
                return Failure(
                    ArtifactStoreFailureKind.UnsafePath,
                    "ARTIFACT_TEMPORARY_FILE_UNSAFE");
            }

            var temporaryInfo =
                new FileInfo(temporaryPath);

            if (temporaryInfo.Length !=
                request.Content.Length)
            {
                return Failure(
                    ArtifactStoreFailureKind.IntegrityFailure,
                    "ARTIFACT_TEMPORARY_LENGTH_MISMATCH");
            }

            try
            {
                File.Move(
                    temporaryPath,
                    destinationPath,
                    overwrite: false);
            }
            catch (IOException)
            {
                if (PathEntryIsLink(destinationPath))
                {
                    return Failure(
                        ArtifactStoreFailureKind.UnsafePath,
                        "ARTIFACT_DESTINATION_IS_LINK");
                }

                if (File.Exists(destinationPath) ||
                    Directory.Exists(destinationPath))
                {
                    return Failure(
                        ArtifactStoreFailureKind.DestinationExists,
                        "ARTIFACT_DESTINATION_EXISTS");
                }

                return Failure(
                    ArtifactStoreFailureKind.IoFailure,
                    "ARTIFACT_PUBLISH_IO_FAILURE");
            }

            ownsTemporary = false;
            ownsPublished = true;
            temporaryPath = null;

            if (!IsSafeRegularFile(destinationPath))
            {
                TryDelete(destinationPath);

                return Failure(
                    ArtifactStoreFailureKind.UnsafePath,
                    "ARTIFACT_PUBLISHED_FILE_UNSAFE");
            }

            string publishedHash;

            await using (var published =
                new FileStream(
                    destinationPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 16_384,
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan))
            {
                publishedHash =
                    Convert.ToHexString(
                        await SHA256.HashDataAsync(
                                published,
                                CancellationToken.None)
                            .ConfigureAwait(false))
                    .ToLowerInvariant();
            }

            var finalInfo =
                new FileInfo(destinationPath);

            if (finalInfo.Length !=
                    request.Content.Length ||
                !FixedTimeHashEquals(
                    expectedHash,
                    publishedHash))
            {
                TryDelete(destinationPath);

                return Failure(
                    ArtifactStoreFailureKind.IntegrityFailure,
                    "ARTIFACT_PUBLISHED_INTEGRITY_FAILURE");
            }

            string relativePath =
                Path.GetRelativePath(
                        rootPath,
                        destinationPath)
                    .Replace(
                        Path.DirectorySeparatorChar,
                        '/');

            if (!IsSafeRelativePath(relativePath))
            {
                TryDelete(destinationPath);

                return Failure(
                    ArtifactStoreFailureKind.UnsafePath,
                    "ARTIFACT_RELATIVE_PATH_UNSAFE");
            }

            ownsPublished = false;

            return ArtifactWriteResult.Success(
                new ArtifactRecord
                {
                    ArtifactId = Guid.NewGuid(),
                    JobId = request.JobId,
                    RunId = request.RunId,
                    ArtifactType = request.ArtifactType,
                    RelativePath = relativePath,
                    Sha256 = publishedHash,
                    SizeBytes = finalInfo.Length,
                    CreatedAtUtc =
                        ToMicrosecondPrecision(
                            DateTimeOffset.UtcNow),
                    CorrelationId =
                        request.CorrelationId
                });
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return Failure(
                ArtifactStoreFailureKind.Cancelled,
                "ARTIFACT_WRITE_CANCELLED");
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(
                ArtifactStoreFailureKind.IoFailure,
                "ARTIFACT_ACCESS_DENIED");
        }
        catch (IOException)
        {
            return Failure(
                ArtifactStoreFailureKind.IoFailure,
                "ARTIFACT_IO_FAILURE");
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                  NotSupportedException or
                  PathTooLongException)
        {
            return Failure(
                ArtifactStoreFailureKind.UnsafePath,
                "ARTIFACT_PATH_INVALID");
        }
        finally
        {
            if (ownsTemporary &&
                temporaryPath is not null)
            {
                TryDelete(temporaryPath);
            }

            if (ownsPublished &&
                destinationPath is not null)
            {
                TryDelete(destinationPath);
            }
        }
    }


    private static DateTimeOffset ToMicrosecondPrecision(
        DateTimeOffset value)
    {
        const long ticksPerMicrosecond = 10;

        long normalizedTicks =
            value.Ticks -
            value.Ticks % ticksPerMicrosecond;

        return new DateTimeOffset(
            normalizedTicks,
            value.Offset);
    }

    private bool IsValidRequest(
        ArtifactWriteRequest? request)
    {
        return request is not null &&
               request.JobId != Guid.Empty &&
               request.RunId != Guid.Empty &&
               Enum.IsDefined(request.ArtifactType) &&
               request.CorrelationId.IndexOfAny(
                   ['\0', '\r', '\n']) < 0;
    }

    private bool EnsureSafeChildDirectory(
        string root,
        string path)
    {
        if (!IsUnderRoot(root, path))
        {
            return false;
        }

        return EnsureSafeDirectory(path);
    }

    private static bool EnsureSafeDirectory(
        string path)
    {
        try
        {
            Directory.CreateDirectory(path);

            var current =
                new DirectoryInfo(
                    Path.GetFullPath(path));

            while (current is not null)
            {
                if (!current.Exists ||
                    IsLink(current))
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

    private static bool IsSafeRegularFile(
        string path)
    {
        try
        {
            var info =
                new FileInfo(path);

            return info.Exists &&
                   !IsLink(info) &&
                   (info.Attributes &
                    FileAttributes.Directory) == 0;
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
                new FileInfo(path);

            if (file.LinkTarget is not null)
            {
                return true;
            }

            if (file.Exists &&
                (file.Attributes &
                 FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }

            var directory =
                new DirectoryInfo(path);

            return directory.LinkTarget is not null ||
                   directory.Exists &&
                   (directory.Attributes &
                    FileAttributes.ReparsePoint) != 0;
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

    private static bool IsLink(
        FileSystemInfo info)
    {
        try
        {
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

    private static bool IsUnderRoot(
        string root,
        string candidate)
    {
        string normalizedRoot =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(root));

        string normalizedCandidate =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(candidate));

        string prefix =
            normalizedRoot +
            Path.DirectorySeparatorChar;

        return normalizedCandidate.StartsWith(
            prefix,
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
    }

    private static bool IsDirectChild(
        string parent,
        string candidate)
    {
        string normalizedParent =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(parent));

        string normalizedCandidate =
            Path.GetFullPath(candidate);

        string? actualParent =
            Path.GetDirectoryName(
                normalizedCandidate);

        return actualParent is not null &&
               string.Equals(
                   normalizedParent,
                   Path.TrimEndingDirectorySeparator(
                       actualParent),
                   OperatingSystem.IsWindows()
                       ? StringComparison.OrdinalIgnoreCase
                       : StringComparison.Ordinal);
    }

    private static bool IsSafeRelativePath(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            Path.IsPathRooted(path) ||
            path.Contains('\\') ||
            path.StartsWith(
                "../",
                StringComparison.Ordinal) ||
            path.Equals(
                "..",
                StringComparison.Ordinal))
        {
            return false;
        }

        return path
            .Split('/')
            .All(segment =>
                segment.Length > 0 &&
                segment is not "." and not "..");
    }

    private static bool FixedTimeHashEquals(
        string left,
        string right)
    {
        try
        {
            return CryptographicOperations
                .FixedTimeEquals(
                    Convert.FromHexString(left),
                    Convert.FromHexString(right));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void TryDelete(
        string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static ArtifactWriteResult Failure(
        ArtifactStoreFailureKind kind,
        string errorCode) =>
        ArtifactWriteResult.Failure(
            kind,
            errorCode);

    private static ArtifactLayout GetLayout(
        ArtifactType type,
        string correlationId) =>
        type switch
        {
            ArtifactType.ContextPackage =>
                new("context", "context.zip"),

            ArtifactType.AiResponse =>
                new("ai", "response.json"),

            ArtifactType.PlanningPlan =>
                new("planner", "plan.json"),

            ArtifactType.PlanningRejectedResponse =>
                new(
                    "planner",
                    PlanningRejectedResponseFileName(
                        correlationId)),

            ArtifactType.DeveloperResponse =>
                new("developer", "response.json"),

            ArtifactType.DeveloperRejectedResponse =>
                new("developer", "rejected-response.json"),

            ArtifactType.DeveloperRejectedStructuredResponse =>
                new("developer", "rejected-structured-response.json"),

            ArtifactType.DeveloperProposal =>
                new("developer", "proposal.json"),

            ArtifactType.DeveloperBuildCorrectionResponse =>
                new("developer", VersionedFileName(
                    "build-correction-response.json", correlationId)),

            ArtifactType.DeveloperBuildCorrectionProposal =>
                new("developer", VersionedFileName(
                    "build-correction-proposal.json", correlationId)),

            ArtifactType.DeveloperBuildCorrectionRetryResponse =>
                new("developer", VersionedFileName(
                    "build-correction-retry-response.json", correlationId)),

            ArtifactType.DeveloperBuildCorrectionRetryProposal =>
                new("developer", VersionedFileName(
                    "build-correction-retry-proposal.json", correlationId)),

            ArtifactType.DeveloperBuildCorrectionRetryRejectedResponse =>
                new("developer", VersionedFileName(
                    "build-correction-retry-rejected-response.json", correlationId)),

            ArtifactType.DeveloperBuildCorrectionRetryRejectedStructuredResponse =>
                new("developer", VersionedFileName(
                    "build-correction-retry-rejected-structured-response.json", correlationId)),

            ArtifactType.DeveloperBuildCorrectionRejectedResponse =>
                new("developer", VersionedFileName(
                    "build-correction-rejected-response.json", correlationId)),

            ArtifactType.DeveloperBuildCorrectionRejectedStructuredResponse =>
                new("developer", VersionedFileName(
                    "build-correction-rejected-structured-response.json", correlationId)),

            ArtifactType.HumanReviewCorrectionEvidence =>
                new("human-review", "changes-required.json"),

            ArtifactType.DeveloperHumanReviewCorrectionResponse =>
                new("developer", VersionedFileName(
                    "human-review-correction-response.json", correlationId)),

            ArtifactType.DeveloperHumanReviewCorrectionProposal =>
                new("developer", VersionedFileName(
                    "human-review-correction-proposal.json", correlationId)),

            ArtifactType.DeveloperHumanReviewCorrectionRejectedResponse =>
                new("developer", VersionedFileName(
                    "human-review-correction-rejected-response.json", correlationId)),

            ArtifactType.DeveloperHumanReviewCorrectionRejectedStructuredResponse =>
                new("developer", VersionedFileName(
                    "human-review-correction-rejected-structured-response.json", correlationId)),

            ArtifactType.ObservedChangeManifest =>
                new("changes", "manifest.json"),

            ArtifactType.ObservedBuildCorrectionManifest =>
                new("changes", "build-correction-manifest.json"),

            ArtifactType.ObservedBuildCorrectionRetryManifest =>
                new("changes", "build-correction-retry-manifest.json"),

            ArtifactType.ObservedHumanReviewCorrectionManifest =>
                new("changes", "human-review-correction-manifest.json"),

            ArtifactType.ReviewerResponse =>
                new("review", "response.json"),

            ArtifactType.ReviewerReview =>
                new("review", "review.json"),

            ArtifactType.ReviewerHumanReviewCorrectionResponse =>
                new("review", "human-review-correction-response.json"),

            ArtifactType.ReviewerHumanReviewCorrectionReview =>
                new("review", "human-review-correction-review.json"),

            ArtifactType.ReviewerHumanReviewCorrectionSupersedingResponse =>
                new("review", "human-review-correction-superseding-response.json"),

            ArtifactType.ReviewerHumanReviewCorrectionSupersedingReview =>
                new("review", "human-review-correction-superseding-review.json"),

            ArtifactType.ReviewerHumanReviewCorrectionSupersessionEvidence =>
                new("review", "human-review-correction-supersession.json"),

            ArtifactType.ReviewerHumanReviewCorrectionSourceAwareSupersedingResponse =>
                new("review", "human-review-correction-source-aware-superseding-v2-response.json"),

            ArtifactType.ReviewerHumanReviewCorrectionSourceAwareSupersedingReview =>
                new("review", "human-review-correction-source-aware-superseding-v2-review.json"),

            ArtifactType.ReviewerHumanReviewCorrectionSourceAwareSupersessionEvidence =>
                new("review", "human-review-correction-source-aware-supersession-v2.json"),

            ArtifactType.ReviewerHumanReviewCorrectionDeterministicSupersedingResponse =>
                new("review", "human-review-correction-deterministic-superseding-v3-response.json"),

            ArtifactType.ReviewerHumanReviewCorrectionDeterministicSupersedingReview =>
                new("review", "human-review-correction-deterministic-superseding-v3-review.json"),

            ArtifactType.ReviewerHumanReviewCorrectionDeterministicSupersessionEvidence =>
                new("review", "human-review-correction-deterministic-supersession-v3.json"),

            ArtifactType.HumanReviewApprovalEvidence =>
                new("human-review", "approval.json"),

            ArtifactType.GovernedHumanCorrectionRequest =>
                new("human-correction", VersionedFileName(
                    "request.json", correlationId)),

            ArtifactType.GovernedHumanCorrectionReceipt =>
                new("human-correction", VersionedFileName(
                    "receipt.json", correlationId)),

            ArtifactType.ObservedGovernedHumanCorrectionManifest =>
                new("human-correction", VersionedFileName(
                    "observed-changes.json", correlationId)),

            ArtifactType.RestoreReport =>
                new("restore", "report.json"),

            ArtifactType.RestoreStandardOutput =>
                new("restore", "stdout.txt"),

            ArtifactType.RestoreStandardError =>
                new("restore", "stderr.txt"),

            ArtifactType.BuildReport =>
                new("build", "report.json"),

            ArtifactType.BuildStandardOutput =>
                new("build", "stdout.txt"),

            ArtifactType.BuildStandardError =>
                new("build", "stderr.txt"),

            ArtifactType.BuildCorrectionReport =>
                new("build", "correction-report.json"),

            ArtifactType.BuildCorrectionStandardOutput =>
                new("build", "correction-stdout.txt"),

            ArtifactType.BuildCorrectionStandardError =>
                new("build", "correction-stderr.txt"),

            ArtifactType.BuildCorrectionRetryReport =>
                new("build", "correction-retry-report.json"),

            ArtifactType.BuildCorrectionRetryStandardOutput =>
                new("build", "correction-retry-stdout.txt"),

            ArtifactType.BuildCorrectionRetryStandardError =>
                new("build", "correction-retry-stderr.txt"),

            ArtifactType.BuildHumanReviewCorrectionReport =>
                new("build", "human-review-correction-report.json"),

            ArtifactType.BuildHumanReviewCorrectionStandardOutput =>
                new("build", "human-review-correction-stdout.txt"),

            ArtifactType.BuildHumanReviewCorrectionStandardError =>
                new("build", "human-review-correction-stderr.txt"),

            ArtifactType.TestReport =>
                new("tests", "report.json"),

            ArtifactType.TestResults =>
                new("tests", "results.zip"),

            ArtifactType.TestStandardOutput =>
                new("tests", "stdout.txt"),

            ArtifactType.TestStandardError =>
                new("tests", "stderr.txt"),

            ArtifactType.TestHumanReviewCorrectionReport =>
                new("tests", "human-review-correction-report.json"),

            ArtifactType.TestHumanReviewCorrectionResults =>
                new("tests", "human-review-correction-results.zip"),

            ArtifactType.TestHumanReviewCorrectionStandardOutput =>
                new("tests", "human-review-correction-stdout.txt"),

            ArtifactType.TestHumanReviewCorrectionStandardError =>
                new("tests", "human-review-correction-stderr.txt"),

            ArtifactType.GeneralReport =>
                new("reports", "report.json"),

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(type))
        };

    private static string PlanningRejectedResponseFileName(
        string correlationId)
    {
        return VersionedFileName(
            "rejected-response.json",
            correlationId);
    }

    private static string VersionedFileName(
        string legacyFileName,
        string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return legacyFileName;
        }

        string identity = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(correlationId)))
            .ToLowerInvariant();
        string extension = Path.GetExtension(legacyFileName);
        string stem = Path.GetFileNameWithoutExtension(legacyFileName);
        return $"{stem}-{identity}{extension}";
    }

    private sealed record ArtifactLayout(
        string Directory,
        string FileName);
}
