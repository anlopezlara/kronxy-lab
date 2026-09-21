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
                ArtifactType = request.IsHumanReviewCorrection
                    ? ArtifactType.ObservedHumanReviewCorrectionManifest
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
