using System.Security.Cryptography;
using Kronxy.Application.Artifacts;

namespace Kronxy.Infrastructure.Artifacts;

public sealed class FileSystemArtifactReader :
    IArtifactReader
{
    private readonly ArtifactStoreOptions options;
    private readonly IArtifactMetadataRepository
        metadataRepository;
    private readonly string rootPath;

    public FileSystemArtifactReader(
        ArtifactStoreOptions options,
        IArtifactMetadataRepository metadataRepository)
    {
        this.options =
            options ??
            throw new ArgumentNullException(
                nameof(options));

        this.metadataRepository =
            metadataRepository ??
            throw new ArgumentNullException(
                nameof(metadataRepository));

        this.options.Validate();

        rootPath =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(
                    options.RootPath));
    }

    public async Task<ArtifactReadResult> ReadAsync(
        ArtifactReadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidRequest(request))
        {
            return Failure(
                ArtifactReadFailureKind.InvalidRequest,
                "ARTIFACT_READ_INVALID_REQUEST");
        }

        long effectiveLimit =
            request.MaxBytes > 0
                ? Math.Min(
                    request.MaxBytes,
                    options.MaxArtifactBytes)
                : options.MaxArtifactBytes;

        try
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (!IsSafeDirectory(rootPath))
            {
                return Failure(
                    ArtifactReadFailureKind.UnsafeRoot,
                    "ARTIFACT_READ_ROOT_UNSAFE");
            }

            IReadOnlyList<ArtifactRecord> artifacts =
                await metadataRepository
                    .GetByJobAndRunAsync(
                        request.JobId,
                        request.RunId,
                        cancellationToken)
                    .ConfigureAwait(false);

            ArtifactRecord[] matching =
                artifacts
                    .Where(
                        artifact =>
                            artifact.ArtifactType ==
                                request.ArtifactType)
                    .ToArray();

            if (request.ArtifactType ==
                ArtifactType.PlanningRejectedResponse)
            {
                matching = SelectPlanningRejectedResponse(
                    matching,
                    request.CorrelationId);
            }
            else if (request.ArtifactType
                .IsVersionedDeveloperCorrection())
            {
                matching = SelectVersionedDeveloperCorrection(
                    matching,
                    request.CorrelationId);
            }
            else if (request.ArtifactType.IsVersionedHumanCorrection() ||
                request.ArtifactType.IsVersionedGovernedDecision())
            {
                matching = SelectExactVersionedArtifact(
                    matching,
                    request.CorrelationId);
            }

            if (matching.Length == 0)
            {
                return Failure(
                    ArtifactReadFailureKind.NotFound,
                    "ARTIFACT_READ_NOT_FOUND");
            }

            if (matching.Length != 1)
            {
                return Failure(
                    ArtifactReadFailureKind.IntegrityFailure,
                    "ARTIFACT_READ_METADATA_AMBIGUOUS");
            }

            ArtifactRecord artifact =
                matching[0];

            if (!IsSafeRelativePath(
                    artifact.RelativePath))
            {
                return Failure(
                    ArtifactReadFailureKind.UnsafePath,
                    "ARTIFACT_READ_RELATIVE_PATH_UNSAFE");
            }

            string candidate =
                Path.GetFullPath(
                    Path.Combine(
                        rootPath,
                        artifact.RelativePath
                            .Replace(
                                '/',
                                Path.DirectorySeparatorChar)));

            if (!IsUnderRoot(
                    rootPath,
                    candidate))
            {
                return Failure(
                    ArtifactReadFailureKind.UnsafePath,
                    "ARTIFACT_READ_PATH_ESCAPE");
            }

            if (!HasSafePathChain(
                    rootPath,
                    candidate))
            {
                return Failure(
                    ArtifactReadFailureKind.SymlinkEscape,
                    "ARTIFACT_READ_SYMLINK_ESCAPE");
            }

            if (!IsSafeRegularFile(candidate))
            {
                return Failure(
                    ArtifactReadFailureKind.NotFound,
                    "ARTIFACT_READ_FILE_NOT_FOUND");
            }

            var info =
                new FileInfo(candidate);

            if (artifact.SizeBytes < 0 ||
                info.Length != artifact.SizeBytes)
            {
                return Failure(
                    ArtifactReadFailureKind.IntegrityFailure,
                    "ARTIFACT_READ_LENGTH_MISMATCH");
            }

            if (info.Length > effectiveLimit ||
                info.Length > int.MaxValue)
            {
                return Failure(
                    ArtifactReadFailureKind.TooLarge,
                    "ARTIFACT_READ_TOO_LARGE");
            }

            byte[] content =
                new byte[(int)info.Length];

            await using (
                var stream =
                    new FileStream(
                        candidate,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 16_384,
                        FileOptions.Asynchronous |
                        FileOptions.SequentialScan))
            {
                int offset =
                    0;

                while (offset < content.Length)
                {
                    int read =
                        await stream
                            .ReadAsync(
                                content.AsMemory(
                                    offset),
                                cancellationToken)
                            .ConfigureAwait(false);

                    if (read == 0)
                    {
                        break;
                    }

                    offset += read;
                }

                if (offset != content.Length)
                {
                    return Failure(
                        ArtifactReadFailureKind.IntegrityFailure,
                        "ARTIFACT_READ_SHORT_READ");
                }
            }

            cancellationToken
                .ThrowIfCancellationRequested();

            if (!HasSafePathChain(
                    rootPath,
                    candidate) ||
                !IsSafeRegularFile(candidate))
            {
                return Failure(
                    ArtifactReadFailureKind.SymlinkEscape,
                    "ARTIFACT_READ_PATH_CHANGED");
            }

            var finalInfo =
                new FileInfo(candidate);

            if (finalInfo.Length !=
                content.LongLength)
            {
                return Failure(
                    ArtifactReadFailureKind.IntegrityFailure,
                    "ARTIFACT_READ_LENGTH_CHANGED");
            }

            string hash =
                Convert.ToHexString(
                    SHA256.HashData(content))
                .ToLowerInvariant();

            if (!FixedTimeHashEquals(
                    artifact.Sha256,
                    hash))
            {
                return Failure(
                    ArtifactReadFailureKind.IntegrityFailure,
                    "ARTIFACT_READ_HASH_MISMATCH");
            }

            return ArtifactReadResult.Success(
                artifact,
                content);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            return Failure(
                ArtifactReadFailureKind.Cancelled,
                "ARTIFACT_READ_CANCELLED");
        }
        catch (IOException)
        {
            return Failure(
                ArtifactReadFailureKind.IoFailure,
                "ARTIFACT_READ_IO_FAILURE");
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(
                ArtifactReadFailureKind.IoFailure,
                "ARTIFACT_READ_ACCESS_DENIED");
        }
        catch (Exception exception)
            when (exception is not
                OutOfMemoryException and not
                StackOverflowException)
        {
            return Failure(
                ArtifactReadFailureKind.IoFailure,
                "ARTIFACT_READ_INTERNAL_FAILURE");
        }
    }

    public async Task<ArtifactReadResult> ReadByIdAsync(
        Guid artifactId, Guid jobId, Guid runId, long maxBytes = 0,
        CancellationToken cancellationToken = default)
    {
        if (artifactId == Guid.Empty || jobId == Guid.Empty || runId == Guid.Empty)
            return Failure(ArtifactReadFailureKind.InvalidRequest, "ARTIFACT_READ_INVALID_REQUEST");

        ArtifactRecord? metadata = await metadataRepository.GetByIdAsync(
            artifactId, cancellationToken).ConfigureAwait(false);
        if (metadata is null || metadata.JobId != jobId || metadata.RunId != runId)
            return Failure(ArtifactReadFailureKind.NotFound, "ARTIFACT_READ_NOT_FOUND");

        return await ReadExactAsync(metadata, maxBytes, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ArtifactReadResult> ReadExactAsync(
        ArtifactRecord artifact, long maxBytes, CancellationToken cancellationToken)
    {
        long limit = maxBytes > 0 ? Math.Min(maxBytes, options.MaxArtifactBytes) : options.MaxArtifactBytes;
        try
        {
            if (!IsSafeDirectory(rootPath) || !IsSafeRelativePath(artifact.RelativePath))
                return Failure(ArtifactReadFailureKind.UnsafePath, "ARTIFACT_READ_PATH_UNSAFE");
            string candidate = Path.GetFullPath(Path.Combine(rootPath,
                artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsUnderRoot(rootPath, candidate) || !HasSafePathChain(rootPath, candidate))
                return Failure(ArtifactReadFailureKind.UnsafePath, "ARTIFACT_READ_PATH_ESCAPE");
            if (!IsSafeRegularFile(candidate))
                return Failure(ArtifactReadFailureKind.NotFound, "ARTIFACT_READ_FILE_NOT_FOUND");
            var info = new FileInfo(candidate);
            if (info.Length > limit)
                return Failure(ArtifactReadFailureKind.TooLarge, "ARTIFACT_READ_TOO_LARGE");
            byte[] content = await File.ReadAllBytesAsync(candidate, cancellationToken);
            string sha = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            if (!string.Equals(sha, artifact.Sha256, StringComparison.OrdinalIgnoreCase))
                return Failure(ArtifactReadFailureKind.IntegrityFailure, "ARTIFACT_READ_INTEGRITY_FAILURE");
            return ArtifactReadResult.Success(artifact, content);
        }
        catch (OperationCanceledException) { return Failure(ArtifactReadFailureKind.Cancelled, "ARTIFACT_READ_CANCELLED"); }
        catch (IOException) { return Failure(ArtifactReadFailureKind.IoFailure, "ARTIFACT_READ_IO_FAILURE"); }
        catch (UnauthorizedAccessException) { return Failure(ArtifactReadFailureKind.UnsafePath, "ARTIFACT_READ_ACCESS_DENIED"); }
    }

    private static ArtifactRecord[]
        SelectPlanningRejectedResponse(
            IReadOnlyList<ArtifactRecord> artifacts,
            string correlationId)
    {
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            return artifacts
                .Where(artifact =>
                    string.Equals(
                        artifact.CorrelationId,
                        correlationId,
                        StringComparison.Ordinal))
                .ToArray();
        }

        ArtifactRecord[] legacy = artifacts
            .Where(artifact =>
                artifact.RelativePath.EndsWith(
                    "/planner/rejected-response.json",
                    StringComparison.Ordinal))
            .ToArray();

        if (legacy.Length > 0)
        {
            return legacy;
        }

        return artifacts
            .OrderByDescending(artifact =>
                artifact.CreatedAtUtc)
            .ThenByDescending(artifact =>
                artifact.ArtifactId)
            .Take(1)
            .ToArray();
    }

    private static ArtifactRecord[]
        SelectVersionedDeveloperCorrection(
            IReadOnlyList<ArtifactRecord> artifacts,
            string correlationId)
    {
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            ArtifactRecord[] exact = artifacts
                .Where(artifact => string.Equals(
                    artifact.CorrelationId,
                    correlationId,
                    StringComparison.Ordinal))
                .ToArray();
            if (exact.Length > 0)
            {
                return exact;
            }
        }

        return artifacts
            .OrderByDescending(artifact =>
                artifact.CreatedAtUtc)
            .ThenByDescending(artifact =>
                artifact.ArtifactId)
            .Take(1)
            .ToArray();
    }

    private static ArtifactRecord[] SelectExactVersionedArtifact(
        IReadOnlyList<ArtifactRecord> artifacts,
        string correlationId) =>
        string.IsNullOrWhiteSpace(correlationId)
            ? artifacts
                .OrderByDescending(artifact => artifact.CreatedAtUtc)
                .ThenByDescending(artifact => artifact.ArtifactId)
                .Take(1)
                .ToArray()
            : artifacts
                .Where(artifact => string.Equals(
                    artifact.CorrelationId,
                    correlationId,
                    StringComparison.Ordinal))
                .ToArray();

    private static bool IsValidRequest(
        ArtifactReadRequest? request)
    {
        return request is not null &&
               request.JobId != Guid.Empty &&
               request.RunId != Guid.Empty &&
               Enum.IsDefined(
                   request.ArtifactType) &&
               request.MaxBytes >= 0 &&
               request.CorrelationId.IndexOfAny(
                   ['\0', '\r', '\n']) < 0;
    }

    private static bool IsSafeDirectory(
        string path)
    {
        try
        {
            var info =
                new DirectoryInfo(
                    Path.GetFullPath(path));

            return info.Exists &&
                   !IsLink(info);
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

    private static bool HasSafePathChain(
        string root,
        string candidate)
    {
        try
        {
            string normalizedRoot =
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(root));

            string normalizedCandidate =
                Path.GetFullPath(candidate);

            if (!IsUnderRoot(
                    normalizedRoot,
                    normalizedCandidate))
            {
                return false;
            }

            DirectoryInfo? current =
                new FileInfo(
                    normalizedCandidate)
                    .Directory;

            while (current is not null)
            {
                string currentPath =
                    Path.TrimEndingDirectorySeparator(
                        current.FullName);

                if (IsLink(current))
                {
                    return false;
                }

                if (string.Equals(
                        currentPath,
                        normalizedRoot,
                        PathComparison))
                {
                    return true;
                }

                current =
                    current.Parent;
            }

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
            PathComparison);
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
            .All(
                segment =>
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

    private static StringComparison
        PathComparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static ArtifactReadResult Failure(
        ArtifactReadFailureKind kind,
        string errorCode) =>
        ArtifactReadResult.Failure(
            kind,
            errorCode);
}
