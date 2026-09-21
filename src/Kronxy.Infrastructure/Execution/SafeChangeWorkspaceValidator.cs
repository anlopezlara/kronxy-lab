using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using Kronxy.Application.Execution;

namespace Kronxy.Infrastructure.Execution;

public sealed record SafeChangeTarget(
    ValidatedDeveloperChange Change,
    string DestinationPath,
    string? ExistingSha256);

public sealed record SafeChangeWorkspaceValidationResult(
    IReadOnlyList<SafeChangeTarget>? Targets,
    SafeChangeApplicationFailureKind FailureKind,
    string ErrorCode)
{
    public bool IsSuccess =>
        FailureKind ==
            SafeChangeApplicationFailureKind.None &&
        Targets is not null &&
        Targets.Count > 0 &&
        string.IsNullOrEmpty(ErrorCode);

    public static SafeChangeWorkspaceValidationResult Success(
        IReadOnlyList<SafeChangeTarget> targets)
    {
        return new SafeChangeWorkspaceValidationResult(
            targets,
            SafeChangeApplicationFailureKind.None,
            string.Empty);
    }

    public static SafeChangeWorkspaceValidationResult Failure(
        SafeChangeApplicationFailureKind kind,
        string errorCode)
    {
        return new SafeChangeWorkspaceValidationResult(
            null,
            kind,
            errorCode);
    }
}

public sealed class SafeChangeWorkspaceValidator :
    IDeveloperProposalMetadataBinder
{
    private const string WorkspaceMarker =
        ".kronxy-workspace.json";

    private readonly string authoritativeRepositoryRoot;
    private readonly string workspaceRoot;
    private readonly IDeveloperProposalPolicy proposalPolicy;

    public SafeChangeWorkspaceValidator(
        ExecutionPlaneOptions executionOptions,
        IDeveloperProposalPolicy proposalPolicy)
    {
        ArgumentNullException.ThrowIfNull(
            executionOptions);

        executionOptions.Validate();

        authoritativeRepositoryRoot =
            NormalizeAbsolutePath(
                executionOptions.RepositoryRoot);

        workspaceRoot =
            NormalizeAbsolutePath(
                executionOptions.WorkspaceRoot);

        this.proposalPolicy =
            proposalPolicy ??
            throw new ArgumentNullException(
                nameof(proposalPolicy));
    }

    public async Task<DeveloperProposalMetadataBindingResult>
        BindAsync(
            DeveloperProposalMetadataBindingRequest request,
            CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return BindingFailure(
                "DEVELOPER_METADATA_BINDING_CANCELLED");
        }

        if (request is null ||
            request.JobId == Guid.Empty ||
            request.Repository is null ||
            request.Repository.JobId != request.JobId ||
            request.Proposal is null ||
            request.Proposal.Changes is null ||
            request.Proposal.Changes.Count == 0 ||
            request.AllowedPaths is null ||
            request.AllowedPaths.Count == 0)
        {
            return BindingFailure(
                "DEVELOPER_METADATA_BINDING_INVALID");
        }

        string workspacePath;
        string repositoryPath;

        try
        {
            workspacePath = NormalizeAbsolutePath(
                request.Repository.WorkspacePath);
            repositoryPath = NormalizeAbsolutePath(
                request.Repository.RepositoryPath);
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                  NotSupportedException or
                  PathTooLongException)
        {
            return BindingFailure(
                "DEVELOPER_WORKSPACE_PATH_INVALID");
        }

        if (!Directory.Exists(workspacePath) ||
            !Directory.Exists(repositoryPath) ||
            !IsDirectChild(workspaceRoot, workspacePath) ||
            !IsDirectChild(workspacePath, repositoryPath) ||
            PathEquals(repositoryPath, authoritativeRepositoryRoot))
        {
            return BindingFailure(
                "DEVELOPER_WORKSPACE_UNSAFE");
        }

        if (ContainsLinkInPath(workspacePath) ||
            IsLink(new DirectoryInfo(repositoryPath)) ||
            !ValidateWorkspaceOwnership(
                request.JobId,
                request.Repository,
                workspacePath))
        {
            return BindingFailure(
                "DEVELOPER_WORKSPACE_OWNERSHIP_INVALID");
        }

        var changes = new List<DeveloperChangeOperation>(
            request.Proposal.Changes.Count);

        try
        {
            foreach (DeveloperChangeOperation change
                in request.Proposal.Changes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (change is null ||
                    !IsSafeRelativePath(change.RelativePath))
                {
                    return BindingFailure(
                        "DEVELOPER_PATH_INVALID");
                }

                if (!request.AllowedPaths.Contains(
                        change.RelativePath,
                        StringComparer.OrdinalIgnoreCase))
                {
                    return BindingFailure(
                        "DEVELOPER_PATH_NOT_IN_PLAN");
                }

                string destination = Path.GetFullPath(
                    Path.Combine(
                        repositoryPath,
                        change.RelativePath.Replace(
                            '/',
                            Path.DirectorySeparatorChar)));

                if (!IsUnderRoot(repositoryPath, destination) ||
                    TargetPathContainsLink(
                        repositoryPath,
                        change.RelativePath) ||
                    HasNonDirectoryAncestor(
                        repositoryPath,
                        change.RelativePath))
                {
                    return BindingFailure(
                        "DEVELOPER_PATH_UNSAFE");
                }

                string expectedHash = string.Empty;

                if (change.Operation ==
                    DeveloperChangeOperationType.ReplaceFile)
                {
                    if (!IsSafeRegularFile(destination))
                    {
                        return BindingFailure(
                            "DEVELOPER_REPLACE_SOURCE_INVALID");
                    }

                    expectedHash = await HashFileAsync(
                            destination,
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (!IsSafeRegularFile(destination))
                    {
                        return BindingFailure(
                            "DEVELOPER_REPLACE_SOURCE_CHANGED");
                    }
                }

                changes.Add(change with
                {
                    ExpectedContentSha256 = expectedHash
                });
            }

            return DeveloperProposalMetadataBindingResult.Success(
                request.Proposal with
                {
                    Changes = Array.AsReadOnly(changes.ToArray())
                });
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return BindingFailure(
                "DEVELOPER_METADATA_BINDING_CANCELLED");
        }
        catch (UnauthorizedAccessException)
        {
            return BindingFailure(
                "DEVELOPER_METADATA_BINDING_ACCESS_DENIED");
        }
        catch (IOException)
        {
            return BindingFailure(
                "DEVELOPER_METADATA_BINDING_IO_FAILURE");
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                  NotSupportedException or
                  PathTooLongException)
        {
            return BindingFailure(
                "DEVELOPER_PATH_INVALID");
        }
    }

    public async Task<
        SafeChangeWorkspaceValidationResult>
        ValidateAsync(
            SafeChangeApplicationRequest? request,
            CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Failure(
                SafeChangeApplicationFailureKind.Cancelled,
                "SAFE_CHANGE_VALIDATION_CANCELLED");
        }

        if (!IsValidRequest(
                request))
        {
            return Failure(
                SafeChangeApplicationFailureKind.InvalidRequest,
                "SAFE_CHANGE_INVALID_REQUEST");
        }

        SafeChangeApplicationRequest validRequest =
            request;

        DeveloperProposalPolicyResult proposalResult =
            proposalPolicy.Validate(
                RebuildProposal(
                    validRequest.Proposal));

        if (!proposalResult.IsSuccess ||
            proposalResult.Proposal is null)
        {
            return MapProposalFailure(
                proposalResult.FailureKind);
        }

        string workspacePath;
        string repositoryPath;

        try
        {
            workspacePath =
                NormalizeAbsolutePath(
                    validRequest.Repository
                        .WorkspacePath);

            repositoryPath =
                NormalizeAbsolutePath(
                    validRequest.Repository
                        .RepositoryPath);
        }
        catch (
            Exception exception)
            when (exception is ArgumentException or
                  NotSupportedException or
                  PathTooLongException)
        {
            return Failure(
                SafeChangeApplicationFailureKind
                    .UnsafeWorkspace,
                "SAFE_CHANGE_WORKSPACE_PATH_INVALID");
        }

        if (!Directory.Exists(
                workspacePath) ||
            !Directory.Exists(
                repositoryPath) ||
            !IsDirectChild(
                workspaceRoot,
                workspacePath) ||
            !IsDirectChild(
                workspacePath,
                repositoryPath) ||
            PathEquals(
                repositoryPath,
                authoritativeRepositoryRoot))
        {
            return Failure(
                SafeChangeApplicationFailureKind
                    .UnsafeWorkspace,
                "SAFE_CHANGE_WORKSPACE_UNSAFE");
        }

        if (ContainsLinkInPath(
                workspacePath) ||
            IsLink(
                new DirectoryInfo(
                    repositoryPath)))
        {
            return Failure(
                SafeChangeApplicationFailureKind
                    .SymlinkDetected,
                "SAFE_CHANGE_SYMLINK_DETECTED");
        }

        if (!ValidateWorkspaceOwnership(
                validRequest.JobId,
                validRequest.Repository,
                workspacePath))
        {
            return Failure(
                SafeChangeApplicationFailureKind
                    .UnsafeWorkspace,
                "SAFE_CHANGE_WORKSPACE_OWNERSHIP_INVALID");
        }

        var targets =
            new List<SafeChangeTarget>(
                proposalResult.Proposal
                    .Changes.Count);

        var destinations =
            new HashSet<string>(
                OperatingSystem.IsWindows()
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal);

        try
        {
            foreach (
                ValidatedDeveloperChange change
                in proposalResult.Proposal.Changes)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                if (!IsSafeRelativePath(
                        change.RelativePath))
                {
                    return Failure(
                        SafeChangeApplicationFailureKind
                            .InvalidPath,
                        "SAFE_CHANGE_PATH_INVALID");
                }

                string destination =
                    Path.GetFullPath(
                        Path.Combine(
                            repositoryPath,
                            change.RelativePath.Replace(
                                '/',
                                Path.DirectorySeparatorChar)));

                if (!IsUnderRoot(
                        repositoryPath,
                        destination))
                {
                    return Failure(
                        SafeChangeApplicationFailureKind
                            .InvalidPath,
                        "SAFE_CHANGE_PATH_OUTSIDE_REPOSITORY");
                }

                if (!destinations.Add(
                        destination))
                {
                    return Failure(
                        SafeChangeApplicationFailureKind
                            .InvalidPath,
                        "SAFE_CHANGE_PATH_DUPLICATE");
                }

                if (TargetPathContainsLink(
                        repositoryPath,
                        change.RelativePath))
                {
                    return Failure(
                        SafeChangeApplicationFailureKind
                            .SymlinkDetected,
                        "SAFE_CHANGE_TARGET_SYMLINK_DETECTED");
                }

                if (HasNonDirectoryAncestor(
                        repositoryPath,
                        change.RelativePath))
                {
                    return Failure(
                        SafeChangeApplicationFailureKind
                            .InvalidPath,
                        "SAFE_CHANGE_PARENT_PATH_INVALID");
                }

                if (change.Operation ==
                    DeveloperChangeOperationType.CreateFile)
                {
                    if (PathEntryExists(
                            destination))
                    {
                        return Failure(
                            SafeChangeApplicationFailureKind
                                .DestinationConflict,
                            "SAFE_CHANGE_CREATE_DESTINATION_EXISTS");
                    }

                    targets.Add(
                        new SafeChangeTarget(
                            change,
                            destination,
                            null));

                    continue;
                }

                if (!IsSafeRegularFile(
                        destination))
                {
                    return Failure(
                        SafeChangeApplicationFailureKind
                            .PreconditionFailed,
                        "SAFE_CHANGE_REPLACE_SOURCE_INVALID");
                }

                string existingHash =
                    await HashFileAsync(
                            destination,
                            cancellationToken)
                        .ConfigureAwait(false);

                if (!FixedTimeHashEquals(
                        change.ExpectedContentSha256,
                        existingHash))
                {
                    return Failure(
                        SafeChangeApplicationFailureKind
                            .PreconditionFailed,
                        "SAFE_CHANGE_REPLACE_HASH_MISMATCH");
                }

                if (!IsSafeRegularFile(
                        destination))
                {
                    return Failure(
                        SafeChangeApplicationFailureKind
                            .PreconditionFailed,
                        "SAFE_CHANGE_REPLACE_SOURCE_CHANGED");
                }

                targets.Add(
                    new SafeChangeTarget(
                        change,
                        destination,
                        existingHash));
            }

            return SafeChangeWorkspaceValidationResult
                .Success(
                    Array.AsReadOnly(
                        targets.ToArray()));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return Failure(
                SafeChangeApplicationFailureKind.Cancelled,
                "SAFE_CHANGE_VALIDATION_CANCELLED");
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(
                SafeChangeApplicationFailureKind.IoFailure,
                "SAFE_CHANGE_VALIDATION_ACCESS_DENIED");
        }
        catch (IOException)
        {
            return Failure(
                SafeChangeApplicationFailureKind.IoFailure,
                "SAFE_CHANGE_VALIDATION_IO_FAILURE");
        }
        catch (
            Exception exception)
            when (exception is ArgumentException or
                  NotSupportedException or
                  PathTooLongException)
        {
            return Failure(
                SafeChangeApplicationFailureKind.InvalidPath,
                "SAFE_CHANGE_PATH_INVALID");
        }
    }

    private static bool IsValidRequest(
        [NotNullWhen(true)]
        SafeChangeApplicationRequest? request)
    {
        return request is not null &&
               request.JobId != Guid.Empty &&
               request.RunId != Guid.Empty &&
               request.Repository is not null &&
               request.Proposal is not null &&
               request.Proposal.Changes is not null &&
               request.Proposal.Changes.Count > 0 &&
               request.Repository.JobId ==
                   request.JobId &&
               request.CorrelationId.IndexOfAny(
                   ['\0', '\r', '\n']) < 0;
    }

    private static DeveloperProposal RebuildProposal(
        ValidatedDeveloperProposal proposal)
    {
        return new DeveloperProposal
        {
            Summary =
                proposal.Summary,

            Changes =
                proposal.Changes
                    .Select(
                        change =>
                            new DeveloperChangeOperation
                            {
                                Operation =
                                    change.Operation,

                                RelativePath =
                                    change.RelativePath,

                                Intent =
                                    change.Intent,

                                Content =
                                    change.Content,

                                ExpectedContentSha256 =
                                    change
                                        .ExpectedContentSha256
                            })
                    .ToArray(),

            Assumptions =
                proposal.Assumptions.ToArray(),

            Risks =
                proposal.Risks.ToArray()
        };
    }

    private static SafeChangeWorkspaceValidationResult
        MapProposalFailure(
            DeveloperProposalFailureKind kind)
    {
        return kind switch
        {
            DeveloperProposalFailureKind.InvalidPath or
            DeveloperProposalFailureKind.DuplicatePath =>
                Failure(
                    SafeChangeApplicationFailureKind.InvalidPath,
                    "SAFE_CHANGE_PATH_INVALID"),

            DeveloperProposalFailureKind.ProtectedPath =>
                Failure(
                    SafeChangeApplicationFailureKind.ProtectedPath,
                    "SAFE_CHANGE_PATH_PROTECTED"),

            DeveloperProposalFailureKind.TooManyOperations or
            DeveloperProposalFailureKind.TooManyCreatedFiles or
            DeveloperProposalFailureKind.FileTooLarge or
            DeveloperProposalFailureKind
                .TotalChangeBytesExceeded or
            DeveloperProposalFailureKind.ProposalTooLarge =>
                Failure(
                    SafeChangeApplicationFailureKind.LimitExceeded,
                    "SAFE_CHANGE_LIMIT_EXCEEDED"),

            _ =>
                Failure(
                    SafeChangeApplicationFailureKind.InvalidRequest,
                    "SAFE_CHANGE_PROPOSAL_INVALID")
        };
    }

    private static bool ValidateWorkspaceOwnership(
        Guid expectedJobId,
        Kronxy.Application.Repositories.RepositoryWorktreeHandle repository,
        string workspacePath)
    {
        try
        {
            string markerPath =
                Path.Combine(
                    workspacePath,
                    WorkspaceMarker);

            var markerInfo =
                new FileInfo(
                    markerPath);

            if (!markerInfo.Exists ||
                IsLink(
                    markerInfo))
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

            using JsonDocument document =
                JsonDocument.Parse(
                    stream);

            JsonElement root =
                document.RootElement;

            return
                root.TryGetProperty(
                    "Version",
                    out JsonElement version) &&
                version.ValueKind ==
                    JsonValueKind.Number &&
                version.TryGetInt32(
                    out int markerVersion) &&
                markerVersion == 1 &&
                root.TryGetProperty(
                    "JobId",
                    out JsonElement jobIdElement) &&
                jobIdElement.ValueKind ==
                    JsonValueKind.String &&
                jobIdElement.TryGetGuid(
                    out Guid markerJobId) &&
                markerJobId ==
                    expectedJobId &&
                root.TryGetProperty(
                    "JobExternalId",
                    out JsonElement externalId) &&
                externalId.ValueKind ==
                    JsonValueKind.String &&
                string.Equals(
                    externalId.GetString(),
                    repository.JobExternalId,
                    StringComparison.Ordinal);
        }
        catch (
            Exception exception)
            when (exception is JsonException or
                  IOException or
                  UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsSafeRelativePath(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            Path.IsPathRooted(path) ||
            path.Contains('\\') ||
            path.IndexOfAny(
                ['\0', '\r', '\n']) >= 0)
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

    private static bool TargetPathContainsLink(
        string repositoryRoot,
        string relativePath)
    {
        try
        {
            string current =
                repositoryRoot;

            foreach (
                string segment
                in relativePath.Split('/'))
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

    private static bool HasNonDirectoryAncestor(
        string repositoryRoot,
        string relativePath)
    {
        try
        {
            string current =
                repositoryRoot;

            string[] segments =
                relativePath.Split('/');

            for (
                int index = 0;
                index < segments.Length - 1;
                index++)
            {
                current =
                    Path.Combine(
                        current,
                        segments[index]);

                if (File.Exists(
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
                   !IsLink(info) &&
                   (info.Attributes &
                    FileAttributes.Directory) == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool ContainsLinkInPath(
        string path)
    {
        try
        {
            DirectoryInfo? current =
                new(
                    Path.GetFullPath(
                        path));

            while (current is not null)
            {
                if (IsLink(
                        current))
                {
                    return true;
                }

                current =
                    current.Parent;
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

    private static bool IsUnderRoot(
        string root,
        string candidate)
    {
        string normalizedRoot =
            NormalizeAbsolutePath(
                root);

        string normalizedCandidate =
            NormalizeAbsolutePath(
                candidate);

        return normalizedCandidate.StartsWith(
            normalizedRoot +
                Path.DirectorySeparatorChar,
            PathComparison);
    }

    private static bool IsDirectChild(
        string root,
        string candidate)
    {
        string normalizedRoot =
            NormalizeAbsolutePath(
                root);

        string normalizedCandidate =
            NormalizeAbsolutePath(
                candidate);

        string? parent =
            Path.GetDirectoryName(
                normalizedCandidate);

        return parent is not null &&
               string.Equals(
                   normalizedRoot,
                   NormalizeAbsolutePath(
                       parent),
                   PathComparison);
    }

    private static bool PathEquals(
        string left,
        string right)
    {
        return string.Equals(
            NormalizeAbsolutePath(
                left),
            NormalizeAbsolutePath(
                right),
            PathComparison);
    }

    private static string NormalizeAbsolutePath(
        string path)
    {
        return Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(
                path));
    }

    private static async Task<string> HashFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
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
        string left,
        string right)
    {
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

    private static SafeChangeWorkspaceValidationResult
        Failure(
            SafeChangeApplicationFailureKind kind,
            string errorCode)
    {
        return SafeChangeWorkspaceValidationResult
            .Failure(
                kind,
                errorCode);
    }

    private static DeveloperProposalMetadataBindingResult
        BindingFailure(string errorCode) =>
        DeveloperProposalMetadataBindingResult.Failure(errorCode);
}
