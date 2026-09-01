using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Context;
using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;
using Kronxy.Context.Configuration;
using Kronxy.Context.Files;
using Kronxy.Context.Git;
using Kronxy.Context.Models;
using Kronxy.Context.Packaging;
using Microsoft.Extensions.Logging;

namespace Kronxy.Infrastructure.Context;

public sealed class ContextGenerationService :
    IContextGenerationService
{
    private readonly ISecureToolExecutor executor;
    private readonly IArtifactStore artifactStore;
    private readonly ContextOptions options;
    private readonly ILogger<ContextGenerationService>? logger;

    public ContextGenerationService(
        ISecureToolExecutor executor,
        IArtifactStore artifactStore,
        ContextOptions options,
        ILogger<ContextGenerationService>? logger = null)
    {
        this.executor =
            executor ??
            throw new ArgumentNullException(
                nameof(executor));

        this.artifactStore =
            artifactStore ??
            throw new ArgumentNullException(
                nameof(artifactStore));

        this.options =
            options ??
            throw new ArgumentNullException(
                nameof(options));

        this.logger =
            logger;

        IReadOnlyList<string> validation =
            options.Validate();

        if (validation.Count != 0)
        {
            throw new ArgumentException(
                "Context options are invalid.",
                nameof(options));
        }
    }

    public async Task<ContextGenerationResult>
        GenerateAsync(
            ContextGenerationRequest request,
            CancellationToken cancellationToken = default)
    {
        if (!IsValidRequest(request))
        {
            return Failure(
                ContextGenerationFailureKind.InvalidRequest,
                "CONTEXT_INVALID_REQUEST");
        }

        DirectoryInfo? stagingDirectory = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.Equals(
                    request.Repository.Head,
                    request.BaseRepositoryHead,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Failure(
                    ContextGenerationFailureKind.RepositoryMismatch,
                    "CONTEXT_REPOSITORY_HEAD_MISMATCH");
            }

            stagingDirectory =
                Directory.CreateTempSubdirectory(
                    "kronxy-context-");

            string packagePath =
                Path.Combine(
                    stagingDirectory.FullName,
                    "context.zip");

            var gitClient =
                new SecureContextGitClient(
                    executor,
                    request.Repository,
                    request.CorrelationId);

            var inventory =
                new RepositoryFileInventory(
                    gitClient);

            RepositoryInventoryLimits inventoryLimits =
                RepositoryInventoryLimits.FromOptions(
                    options,
                    options.MaxBaselinePackageBytes);

            RepositoryFileInventoryResult inventoryResult =
                await inventory.CreateAsync(
                        request.Repository.RepositoryPath,
                        inventoryLimits,
                        options.DefaultOutputDirectory,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!inventoryResult.IsComplete)
            {
                return Failure(
                    MapInventoryFailure(
                        inventoryResult.Status),
                    "CONTEXT_INVENTORY_" +
                    inventoryResult.Status
                        .ToString()
                        .ToUpperInvariant());
            }

            if (inventoryResult.Accepted.Count == 0)
            {
                return Failure(
                    ContextGenerationFailureKind.InventoryFailure,
                    "CONTEXT_NO_ACCEPTED_FILES");
            }

            var reader =
                new SafeTextFileReader();

            var entries =
                new List<PackageEntryRequest>(
                    inventoryResult.Accepted.Count);

            foreach (
                RepositoryInventoryFile file
                in inventoryResult.Accepted)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                SafeTextReadResult read =
                    await reader.ReadAsync(
                            request.Repository.RepositoryPath,
                            file.LogicalPath,
                            options.MaxTextFileBytes,
                            cancellationToken)
                        .ConfigureAwait(false);

                if (!read.IsSuccess ||
                    read.Content is null)
                {
                    return Failure(
                        MapReadFailure(
                            read.Status),
                        "CONTEXT_FILE_READ_" +
                        read.Status
                            .ToString()
                            .ToUpperInvariant());
                }

                entries.Add(
                    new PackageEntryRequest
                    {
                        LogicalPath =
                            file.LogicalPath,

                        Kind =
                            PackageEntryKind.Source,

                        Content =
                            read.Content
                    });
            }

            var packageBuilder =
                new PackageBuilder();

            var packageLimits =
                new PackageBuildLimits
                {
                    MaxEntries =
                        options.MaxFilesPerPackage,

                    MaxEntryBytes =
                        options.MaxTextFileBytes,

                    MaxTotalContentBytes =
                        options.MaxBaselinePackageBytes,

                    MaxPathBytes =
                        options.MaxLogicalPathLength
                };

            DateTimeOffset generatedAt =
                DateTimeOffset.UtcNow;

            PackageBuildResult build =
                await packageBuilder.BuildAsync(
                        new PackageBuildRequest
                        {
                            DestinationPath =
                                packagePath,

                            PackageType =
                                PackageType.Baseline,

                            Stability =
                                PackageStability.Final,

                            GeneratedAtUtc =
                                generatedAt,

                            Entries =
                                entries,

                            Limits =
                                packageLimits
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!build.IsSuccess)
            {
                return Failure(
                    MapPackageFailure(
                        build.Status),
                    "CONTEXT_PACKAGE_" +
                    build.Status
                        .ToString()
                        .ToUpperInvariant());
            }

            if (build.PackageSha256 is null ||
                build.PackageSizeBytes is null ||
                build.EntryCount != entries.Count)
            {
                return Failure(
                    ContextGenerationFailureKind.InvalidPackage,
                    "CONTEXT_PACKAGE_RESULT_INVALID");
            }

            byte[] packageBytes =
                await File.ReadAllBytesAsync(
                        packagePath,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (packageBytes.LongLength !=
                build.PackageSizeBytes.Value)
            {
                return Failure(
                    ContextGenerationFailureKind.InvalidPackage,
                    "CONTEXT_PACKAGE_SIZE_MISMATCH");
            }

            string packageHash =
                Convert.ToHexString(
                        SHA256.HashData(
                            packageBytes))
                    .ToLowerInvariant();

            if (!string.Equals(
                    packageHash,
                    build.PackageSha256,
                    StringComparison.Ordinal))
            {
                return Failure(
                    ContextGenerationFailureKind.InvalidPackage,
                    "CONTEXT_PACKAGE_HASH_MISMATCH");
            }

            PackageManifestMetadata? manifest =
                ReadManifest(
                    packagePath);

            if (manifest is null ||
                !IsValidManifest(
                    manifest,
                    build.EntryCount))
            {
                return Failure(
                    ContextGenerationFailureKind.InvalidPackage,
                    "CONTEXT_MANIFEST_INVALID");
            }

            ArtifactWriteResult write =
                await artifactStore.WriteAsync(
                        new ArtifactWriteRequest
                        {
                            JobId =
                                request.JobId,

                            RunId =
                                request.RunId,

                            ArtifactType =
                                ArtifactType.ContextPackage,

                            Content =
                                packageBytes,

                            CorrelationId =
                                request.CorrelationId
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!write.IsSuccess ||
                write.Artifact is null)
            {
                return Failure(
                    write.FailureKind ==
                    ArtifactStoreFailureKind.Cancelled
                        ? ContextGenerationFailureKind.Cancelled
                        : ContextGenerationFailureKind.ArtifactWriteFailure,
                    string.IsNullOrWhiteSpace(
                        write.ErrorCode)
                        ? "CONTEXT_ARTIFACT_WRITE_FAILED"
                        : write.ErrorCode);
            }

            ArtifactRecord artifact =
                write.Artifact;

            if (artifact.SizeBytes !=
                    packageBytes.LongLength ||
                !string.Equals(
                    artifact.Sha256,
                    packageHash,
                    StringComparison.Ordinal))
            {
                return Failure(
                    ContextGenerationFailureKind.InvalidPackage,
                    "CONTEXT_ARTIFACT_INTEGRITY_MISMATCH");
            }

            return ContextGenerationResult.Success(
                new ContextPackageArtifact
                {
                    PackageId =
                        manifest.PackageId,

                    RelativePath =
                        artifact.RelativePath,

                    Sha256 =
                        artifact.Sha256,

                    SizeBytes =
                        artifact.SizeBytes,

                    EntryCount =
                        build.EntryCount,

                    GeneratorVersion =
                        manifest.GeneratorVersion,

                    CreatedAtUtc =
                        manifest.GeneratedAtUtc
                });
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            return Failure(
                ContextGenerationFailureKind.Cancelled,
                "CONTEXT_CANCELLED");
        }
        catch (GitClientException exception)
        {
            return Failure(
                exception.Kind ==
                    GitErrorKind.TimedOut
                    ? ContextGenerationFailureKind.TimedOut
                    : ContextGenerationFailureKind.InventoryFailure,
                "CONTEXT_GIT_" +
                exception.Kind
                    .ToString()
                    .ToUpperInvariant());
        }
        catch (IOException)
        {
            return Failure(
                ContextGenerationFailureKind.InternalFailure,
                "CONTEXT_IO_FAILURE");
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(
                ContextGenerationFailureKind.InternalFailure,
                "CONTEXT_ACCESS_DENIED");
        }
        catch (JsonException)
        {
            return Failure(
                ContextGenerationFailureKind.InvalidPackage,
                "CONTEXT_MANIFEST_JSON_INVALID");
        }
        catch (Exception exception)
            when (exception is not
                OutOfMemoryException and not
                StackOverflowException)
        {
            return Failure(
                ContextGenerationFailureKind.InternalFailure,
                "CONTEXT_INTERNAL_FAILURE");
        }
        finally
        {
            if (stagingDirectory is not null)
            {
                TryDeleteStaging(
                    stagingDirectory.FullName);
            }
        }
    }

    private static bool IsValidRequest(
        ContextGenerationRequest? request)
    {
        if (request is null ||
            request.JobId == Guid.Empty ||
            request.RunId == Guid.Empty ||
            request.Repository is null ||
            string.IsNullOrWhiteSpace(
                request.JobExternalId) ||
            string.IsNullOrWhiteSpace(
                request.BaseRepositoryHead))
        {
            return false;
        }

        if (request.CorrelationId.IndexOfAny(
                ['\0', '\r', '\n']) >= 0)
        {
            return false;
        }

        if (request.Repository.JobId !=
                request.JobId ||
            !string.Equals(
                request.Repository.JobExternalId,
                request.JobExternalId,
                StringComparison.Ordinal))
        {
            return false;
        }

        if (!IsCommitHash(
                request.BaseRepositoryHead) ||
            !IsCommitHash(
                request.Repository.Head))
        {
            return false;
        }

        return true;
    }

    private static bool IsCommitHash(
        string value)
    {
        if (value.Length is not 40 and not 64)
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!(
                character is >= '0' and <= '9' or
                >= 'a' and <= 'f' or
                >= 'A' and <= 'F'))
            {
                return false;
            }
        }

        return true;
    }

    private static ContextGenerationFailureKind
        MapInventoryFailure(
            RepositoryInventoryStatus status) =>
        status switch
        {
            RepositoryInventoryStatus
                .CandidateLimitExceeded =>
                    ContextGenerationFailureKind
                        .PackageLimitExceeded,

            RepositoryInventoryStatus
                .AcceptedFileLimitExceeded =>
                    ContextGenerationFailureKind
                        .PackageLimitExceeded,

            RepositoryInventoryStatus
                .TotalSizeLimitExceeded =>
                    ContextGenerationFailureKind
                        .PackageLimitExceeded,

            _ =>
                ContextGenerationFailureKind
                    .InventoryFailure
        };

    private static ContextGenerationFailureKind
        MapReadFailure(
            TextReadStatus status) =>
        status switch
        {
            TextReadStatus.TooLarge =>
                ContextGenerationFailureKind
                    .PackageLimitExceeded,

            TextReadStatus.ReparsePoint or
            TextReadStatus.InvalidPath =>
                ContextGenerationFailureKind
                    .FileReadFailure,

            _ =>
                ContextGenerationFailureKind
                    .FileReadFailure
        };

    private static ContextGenerationFailureKind
        MapPackageFailure(
            PackageBuildStatus status) =>
        status switch
        {
            PackageBuildStatus
                .SensitiveContentRemaining =>
                    ContextGenerationFailureKind
                        .SensitiveContentDetected,

            PackageBuildStatus
                .RedactionFailed =>
                    ContextGenerationFailureKind
                        .SensitiveContentDetected,

            PackageBuildStatus
                .LimitExceeded =>
                    ContextGenerationFailureKind
                        .PackageLimitExceeded,

            PackageBuildStatus
                .Cancelled =>
                    ContextGenerationFailureKind
                        .Cancelled,

            PackageBuildStatus
                .IntegrityFailure or
            PackageBuildStatus
                .InvalidPath or
            PackageBuildStatus
                .DuplicateEntry =>
                    ContextGenerationFailureKind
                        .InvalidPackage,

            _ =>
                ContextGenerationFailureKind
                    .InternalFailure
        };

    private static PackageManifestMetadata?
        ReadManifest(
            string packagePath)
    {
        using ZipArchive archive =
            ZipFile.OpenRead(
                packagePath);

        ZipArchiveEntry? entry =
            archive.GetEntry(
                "manifest.json");

        if (entry is null)
        {
            return null;
        }

        using Stream stream =
            entry.Open();

        using JsonDocument document =
            JsonDocument.Parse(
                stream);

        JsonElement root =
            document.RootElement;

        if (!root.TryGetProperty(
                "packageId",
                out JsonElement packageId) ||
            packageId.ValueKind !=
                JsonValueKind.String ||
            !root.TryGetProperty(
                "generatorVersion",
                out JsonElement generatorVersion) ||
            generatorVersion.ValueKind !=
                JsonValueKind.String ||
            !root.TryGetProperty(
                "generatedAtUtc",
                out JsonElement generatedAtUtc) ||
            generatedAtUtc.ValueKind !=
                JsonValueKind.String ||
            !root.TryGetProperty(
                "fileCount",
                out JsonElement fileCount) ||
            fileCount.ValueKind !=
                JsonValueKind.Number)
        {
            return null;
        }

        string? packageIdValue =
            packageId.GetString();

        string? generatorVersionValue =
            generatorVersion.GetString();

        string? generatedAtValue =
            generatedAtUtc.GetString();

        if (packageIdValue is null ||
            generatorVersionValue is null ||
            generatedAtValue is null ||
            !DateTimeOffset.TryParse(
                generatedAtValue,
                System.Globalization
                    .CultureInfo.InvariantCulture,
                System.Globalization
                    .DateTimeStyles
                    .AssumeUniversal |
                System.Globalization
                    .DateTimeStyles
                    .AdjustToUniversal,
                out DateTimeOffset timestamp))
        {
            return null;
        }

        return new PackageManifestMetadata(
            packageIdValue,
            generatorVersionValue,
            timestamp,
            fileCount.GetInt32());
    }

    private static bool IsValidManifest(
        PackageManifestMetadata manifest,
        int expectedFileCount) =>
        IsCommitHash(
            manifest.PackageId) &&
        !string.IsNullOrWhiteSpace(
            manifest.GeneratorVersion) &&
        manifest.FileCount ==
            expectedFileCount;

    private static void TryDeleteStaging(
        string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(
                    path,
                    recursive: true);
            }
        }
        catch
        {
            // Cleanup is best effort.
            // No artifact path is exposed.
        }
    }

    private ContextGenerationResult Failure(
        ContextGenerationFailureKind kind,
        string errorCode)
    {
        logger?.LogWarning(
            "Context generation failed. FailureKind={FailureKind} ErrorCode={ErrorCode}",
            kind,
            errorCode);

        return ContextGenerationResult.Failure(
            kind,
            errorCode);
    }

    private sealed record PackageManifestMetadata(
        string PackageId,
        string GeneratorVersion,
        DateTimeOffset GeneratedAtUtc,
        int FileCount);
}
