using System.Security.Cryptography;
using System.Text.Json;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;
using Kronxy.Application.Workspaces;

namespace Kronxy.Infrastructure.Execution;

public sealed class ObservedChangeEvidenceService : IObservedChangeEvidenceService
{
    private const int MaxFiles = 256;
    private const long MaxFileBytes = 16 * 1024 * 1024;
    private readonly IRepositoryManager repositoryManager;
    private readonly IArtifactStore artifactStore;

    public ObservedChangeEvidenceService(
        IRepositoryManager repositoryManager,
        IArtifactStore artifactStore)
    {
        this.repositoryManager = repositoryManager ?? throw new ArgumentNullException(nameof(repositoryManager));
        this.artifactStore = artifactStore ?? throw new ArgumentNullException(nameof(artifactStore));
    }

    public async Task<ObservedChangeEvidenceResult> CaptureAsync(
        ObservedChangeEvidenceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.JobId == Guid.Empty || request.RunId == Guid.Empty ||
            request.Repository is null || request.Proposal is null ||
            request.Repository.JobId != request.JobId ||
            string.IsNullOrWhiteSpace(request.Repository.Head))
            return Failure(ObservedChangeEvidenceFailureKind.InvalidRequest, "OBSERVED_CHANGE_INVALID_REQUEST");

        try
        {
            var workspace = new WorkspaceHandle(
                request.JobId,
                request.Repository.JobExternalId,
                request.Repository.WorkspacePath,
                WorkspaceOperationKind.Existing);
            RepositoryOperationResult<IReadOnlyList<ObservedRepositoryChange>> observedResult =
                await repositoryManager.GetObservedChangesAsync(workspace, cancellationToken);

            if (!observedResult.IsSuccess || observedResult.Value is null)
                return Failure(ObservedChangeEvidenceFailureKind.RepositoryFailure, "OBSERVED_CHANGE_REPOSITORY_FAILURE");

            IReadOnlyList<ObservedRepositoryChange> observed = observedResult.Value;
            if (request.SafeChangeChanges.Count > 0)
                return await CaptureProposalScopedAsync(
                    request,
                    observed,
                    cancellationToken);

            if (request.IsGovernedHumanCorrection)
            {
                HashSet<string> allowed = request.AllowedPaths
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (allowed.Count == 0 ||
                    observed.Any(change => !allowed.Contains(change.RelativePath)))
                    return Failure(
                        ObservedChangeEvidenceFailureKind.EvidenceMismatch,
                        "OBSERVED_CHANGE_OUTSIDE_HUMAN_CORRECTION_SCOPE");
                HashSet<string> humanPaths = request.Proposal.Changes
                    .Select(change => change.RelativePath)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                observed = observed
                    .Where(change => humanPaths.Contains(change.RelativePath))
                    .ToArray();
            }
            if (observed.Count > MaxFiles)
                return Failure(ObservedChangeEvidenceFailureKind.EvidenceMismatch, "OBSERVED_CHANGE_LIMIT_EXCEEDED");

            var expected = request.Proposal.Changes.ToDictionary(
                change => change.RelativePath,
                change => change.Operation == DeveloperChangeOperationType.CreateFile
                    ? ObservedRepositoryChangeKind.Created
                    : ObservedRepositoryChangeKind.Modified,
                StringComparer.Ordinal);

            if (observed.Count != expected.Count)
                return Failure(ObservedChangeEvidenceFailureKind.EvidenceMismatch, "OBSERVED_CHANGE_SET_MISMATCH");

            var entries = new List<ObservedChangeManifestEntry>(observed.Count);
            foreach (ObservedRepositoryChange change in observed.OrderBy(value => value.RelativePath, StringComparer.Ordinal))
            {
                if (!expected.TryGetValue(change.RelativePath, out ObservedRepositoryChangeKind expectedKind))
                    return Failure(ObservedChangeEvidenceFailureKind.EvidenceMismatch, "OBSERVED_CHANGE_UNEXPECTED_PATH");
                if (expectedKind != change.ChangeKind)
                    return Failure(ObservedChangeEvidenceFailureKind.EvidenceMismatch, "OBSERVED_CHANGE_KIND_MISMATCH");

                if (change.ChangeKind == ObservedRepositoryChangeKind.Deleted)
                {
                    entries.Add(new(change.RelativePath, change.ChangeKind, null, null));
                    continue;
                }

                string? path = ResolveRegularFile(request.Repository.RepositoryPath, change.RelativePath);
                if (path is null)
                    return Failure(ObservedChangeEvidenceFailureKind.UnsafePath, "OBSERVED_CHANGE_FILE_UNSAFE");

                var info = new FileInfo(path);
                if (info.Length > MaxFileBytes)
                    return Failure(ObservedChangeEvidenceFailureKind.UnsafePath, "OBSERVED_CHANGE_FILE_TOO_LARGE");

                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                    16_384, FileOptions.Asynchronous | FileOptions.SequentialScan);
                byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
                entries.Add(new(change.RelativePath, change.ChangeKind,
                    Convert.ToHexString(hash).ToLowerInvariant(), info.Length));
            }

            var manifest = new ObservedChangeManifest(
                request.JobId, request.RunId, request.Repository.Head, entries);
            byte[] content = JsonSerializer.SerializeToUtf8Bytes(manifest);
            ArtifactWriteResult write = await artifactStore.WriteAsync(new ArtifactWriteRequest
            {
                JobId = request.JobId,
                RunId = request.RunId,
                ArtifactType = request.IsGovernedHumanCorrection
                    ? ArtifactType.ObservedGovernedHumanCorrectionManifest
                    : request.IsHumanReviewCorrection
                    ? ArtifactType.ObservedHumanReviewCorrectionManifest
                    : request.IsBuildCorrectionRetry
                    ? ArtifactType.ObservedBuildCorrectionRetryManifest
                    : request.IsBuildCorrection
                    ? ArtifactType.ObservedBuildCorrectionManifest
                    : ArtifactType.ObservedChangeManifest,
                Content = content,
                CorrelationId = request.CorrelationId
            }, cancellationToken);

            return write.IsSuccess && write.Artifact is not null
                ? ObservedChangeEvidenceResult.Success(manifest, write.Artifact)
                : Failure(ObservedChangeEvidenceFailureKind.ArtifactWriteFailure, "OBSERVED_CHANGE_ARTIFACT_WRITE_FAILED");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure(ObservedChangeEvidenceFailureKind.Cancelled, "OBSERVED_CHANGE_CANCELLED");
        }
        catch (IOException)
        {
            return Failure(ObservedChangeEvidenceFailureKind.IoFailure, "OBSERVED_CHANGE_IO_FAILURE");
        }
    }

    private async Task<ObservedChangeEvidenceResult>
        CaptureProposalScopedAsync(
            ObservedChangeEvidenceRequest request,
            IReadOnlyList<ObservedRepositoryChange> gitObserved,
            CancellationToken cancellationToken)
    {
        if (request.AttemptCount <= 0 ||
            string.IsNullOrWhiteSpace(request.ProposalLineageId) ||
            string.IsNullOrWhiteSpace(request.SafeChangeReceiptReference) ||
            request.ProposalFingerprintSha256.Length != 64 ||
            !string.Equals(
                request.ProposalFingerprintSha256,
                SafeChangeProposalIdentity.Fingerprint(request.Proposal),
                StringComparison.Ordinal) ||
            request.SafeChangeChanges.Count != request.Proposal.Changes.Count)
            return Failure(
                ObservedChangeEvidenceFailureKind.EvidenceMismatch,
                "OBSERVED_CHANGE_SAFECHANGE_LINEAGE_INVALID");

        HashSet<string> proposalPaths = request.Proposal.Changes
            .Select(change => change.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> allowed = request.AllowedPaths.Count > 0
            ? request.AllowedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : proposalPaths;
        if (!proposalPaths.IsSubsetOf(allowed) ||
            gitObserved.Any(change => !allowed.Contains(change.RelativePath)))
            return Failure(
                ObservedChangeEvidenceFailureKind.EvidenceMismatch,
                "OBSERVED_CHANGE_OUTSIDE_PROPOSAL_LINEAGE_SCOPE");

        Dictionary<string, ObservedRepositoryChange> gitByPath;
        try
        {
            gitByPath = gitObserved.ToDictionary(
                change => change.RelativePath,
                StringComparer.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return Failure(
                ObservedChangeEvidenceFailureKind.EvidenceMismatch,
                "OBSERVED_CHANGE_GIT_PATH_DUPLICATE");
        }

        var entries = new List<ObservedChangeManifestEntry>(
            request.Proposal.Changes.Count);
        foreach (ValidatedDeveloperChange proposed in
            request.Proposal.Changes.OrderBy(
                change => change.RelativePath,
                StringComparer.Ordinal))
        {
            AppliedFileChange? applied = request.SafeChangeChanges
                .SingleOrDefault(change => string.Equals(
                    change.RelativePath,
                    proposed.RelativePath,
                    StringComparison.OrdinalIgnoreCase));
            if (applied is null ||
                applied.Operation != proposed.Operation ||
                !gitByPath.TryGetValue(
                    proposed.RelativePath,
                    out ObservedRepositoryChange? gitChange))
                return Failure(
                    ObservedChangeEvidenceFailureKind.EvidenceMismatch,
                    "OBSERVED_CHANGE_SAFECHANGE_RECEIPT_MISMATCH");

            ObservedRepositoryChangeKind semanticKind;
            if (proposed.Operation == DeveloperChangeOperationType.CreateFile)
            {
                if (!string.IsNullOrEmpty(applied.BeforeSha256) ||
                    !string.IsNullOrEmpty(proposed.ExpectedContentSha256))
                    return Failure(
                        ObservedChangeEvidenceFailureKind.EvidenceMismatch,
                        "OBSERVED_CHANGE_CREATE_BEFORE_PRESENT");
                semanticKind = ObservedRepositoryChangeKind.Created;
            }
            else
            {
                if (string.IsNullOrEmpty(applied.BeforeSha256) ||
                    !string.Equals(
                        applied.BeforeSha256,
                        proposed.ExpectedContentSha256,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        applied.BeforeSha256,
                        applied.AfterSha256,
                        StringComparison.OrdinalIgnoreCase))
                    return Failure(
                        ObservedChangeEvidenceFailureKind.EvidenceMismatch,
                        "OBSERVED_CHANGE_REPLACE_HASH_INVALID");
                semanticKind = ObservedRepositoryChangeKind.Modified;
            }

            string? path = ResolveRegularFile(
                request.Repository.RepositoryPath,
                proposed.RelativePath);
            if (path is null)
                return Failure(
                    ObservedChangeEvidenceFailureKind.UnsafePath,
                    "OBSERVED_CHANGE_FILE_UNSAFE");

            var info = new FileInfo(path);
            if (info.Length > MaxFileBytes)
                return Failure(
                    ObservedChangeEvidenceFailureKind.UnsafePath,
                    "OBSERVED_CHANGE_FILE_TOO_LARGE");

            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                16_384,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            string finalHash = Convert.ToHexString(
                    await SHA256.HashDataAsync(stream, cancellationToken))
                .ToLowerInvariant();
            string proposedHash = Convert.ToHexString(
                    SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes(proposed.Content)))
                .ToLowerInvariant();
            if (!string.Equals(
                    finalHash,
                    applied.AfterSha256,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    finalHash,
                    proposedHash,
                    StringComparison.OrdinalIgnoreCase) ||
                info.Length != applied.SizeBytes)
                return Failure(
                    ObservedChangeEvidenceFailureKind.EvidenceMismatch,
                    "OBSERVED_CHANGE_AFTER_HASH_MISMATCH");

            entries.Add(new ObservedChangeManifestEntry(
                proposed.RelativePath,
                semanticKind,
                finalHash,
                info.Length,
                proposed.Operation,
                gitChange.ChangeKind,
                applied.BeforeSha256,
                "PASS"));
        }

        var manifest = new ObservedChangeManifest(
            request.JobId,
            request.RunId,
            request.Repository.Head,
            entries,
            request.AttemptCount,
            request.ProposalLineageId,
            request.ProposalFingerprintSha256,
            request.SafeChangeReceiptReference);
        ArtifactWriteResult write = await artifactStore.WriteAsync(
            new ArtifactWriteRequest
            {
                JobId = request.JobId,
                RunId = request.RunId,
                ArtifactType = request.IsGovernedHumanCorrection
                    ? ArtifactType.ObservedGovernedHumanCorrectionManifest
                    : request.IsHumanReviewCorrection
                        ? ArtifactType.ObservedHumanReviewCorrectionManifest
                        : request.IsBuildCorrectionRetry
                            ? ArtifactType.ObservedBuildCorrectionRetryManifest
                            : request.IsBuildCorrection
                                ? ArtifactType.ObservedBuildCorrectionManifest
                                : ArtifactType.ObservedChangeManifest,
                Content = JsonSerializer.SerializeToUtf8Bytes(manifest),
                CorrelationId = request.CorrelationId
            },
            cancellationToken);

        return write.IsSuccess && write.Artifact is not null
            ? ObservedChangeEvidenceResult.Success(manifest, write.Artifact)
            : Failure(
                ObservedChangeEvidenceFailureKind.ArtifactWriteFailure,
                "OBSERVED_CHANGE_ARTIFACT_WRITE_FAILED");
    }

    private static string? ResolveRegularFile(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) ||
            relativePath.Contains('\\') || relativePath.Split('/').Any(segment => segment is "" or "." or ".."))
            return null;

        string fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        if (!fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            !File.Exists(fullPath) || Directory.Exists(fullPath))
            return null;

        string current = fullRoot;
        foreach (string segment in relativePath.Split('/'))
        {
            current = Path.Combine(current, segment);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                return null;
        }

        return fullPath;
    }

    private static ObservedChangeEvidenceResult Failure(
        ObservedChangeEvidenceFailureKind kind, string code) =>
        ObservedChangeEvidenceResult.Failure(kind, code);
}
