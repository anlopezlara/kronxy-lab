using System.Text.Json;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Execution;
using Kronxy.Infrastructure.Artifacts;

namespace Kronxy.Infrastructure.Execution;

public sealed class StageRecoveryEvidenceService :
    IStageRecoveryEvidenceService
{
    private const long MaxReportBytes =
        1024 * 1024;

    private readonly IArtifactReader artifactReader;
    private readonly ArtifactStoreOptions artifactOptions;

    public StageRecoveryEvidenceService(
        IArtifactReader artifactReader,
        ArtifactStoreOptions artifactOptions)
    {
        this.artifactReader =
            artifactReader ??
            throw new ArgumentNullException(
                nameof(artifactReader));

        this.artifactOptions =
            artifactOptions ??
            throw new ArgumentNullException(
                nameof(artifactOptions));

        this.artifactOptions.Validate();
    }

    public async Task<StageRecoveryResult> CheckAsync(
        StageRecoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.JobId == Guid.Empty ||
            request.RunId == Guid.Empty)
        {
            return StageRecoveryResult.Failure(
                "STAGE_RECOVERY_INVALID_REQUEST");
        }

        try
        {
            return request.Stage switch
            {
                RecoveryStage.Context =>
                    await CheckArtifactAsync(
                        request,
                        ArtifactType.ContextPackage,
                        artifactOptions.MaxArtifactBytes,
                        cancellationToken),

                RecoveryStage.Planning =>
                    await CheckArtifactAsync(
                        request,
                        ArtifactType.AiResponse,
                        artifactOptions.MaxArtifactBytes,
                        cancellationToken),

                RecoveryStage.Restore =>
                    await CheckReportAsync<RestoreExecutionReport>(
                        request,
                        ArtifactType.RestoreReport,
                        report => report.IsSuccess,
                        cancellationToken),

                RecoveryStage.Build =>
                    await CheckReportAsync<BuildExecutionReport>(
                        request,
                        ArtifactType.BuildReport,
                        report => report.IsSuccess,
                        cancellationToken),

                RecoveryStage.Test =>
                    await CheckTestAsync(
                        request,
                        cancellationToken),

                _ =>
                    StageRecoveryResult.Failure(
                        "STAGE_RECOVERY_UNKNOWN_STAGE")
            };
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return StageRecoveryResult.Cancelled(
                "STAGE_RECOVERY_CANCELLED");
        }
        catch
        {
            return StageRecoveryResult.Failure(
                "STAGE_RECOVERY_FAILED");
        }
    }

    private async Task<StageRecoveryResult>
        CheckArtifactAsync(
            StageRecoveryRequest request,
            ArtifactType artifactType,
            long maxBytes,
            CancellationToken cancellationToken)
    {
        ArtifactReadResult read =
            await ReadAsync(
                request,
                artifactType,
                maxBytes,
                cancellationToken);

        if (read.FailureKind ==
            ArtifactReadFailureKind.NotFound)
        {
            return StageRecoveryResult.NotCompleted();
        }

        if (!read.IsSuccess)
        {
            return MapReadFailure(read);
        }

        return StageRecoveryResult.Completed();
    }

    private async Task<StageRecoveryResult>
        CheckReportAsync<TReport>(
            StageRecoveryRequest request,
            ArtifactType artifactType,
            Func<TReport, bool> isSuccess,
            CancellationToken cancellationToken)
        where TReport : class
    {
        ArtifactReadResult read =
            await ReadAsync(
                request,
                artifactType,
                MaxReportBytes,
                cancellationToken);

        if (read.FailureKind ==
            ArtifactReadFailureKind.NotFound)
        {
            return StageRecoveryResult.NotCompleted();
        }

        if (!read.IsSuccess)
        {
            return MapReadFailure(read);
        }

        TReport? report;

        try
        {
            report =
                JsonSerializer.Deserialize<TReport>(
                    read.Content.Span);
        }
        catch (JsonException)
        {
            return StageRecoveryResult.InvalidEvidence(
                "STAGE_RECOVERY_REPORT_INVALID_JSON");
        }

        if (report is null ||
            !isSuccess(report))
        {
            return StageRecoveryResult.InvalidEvidence(
                "STAGE_RECOVERY_REPORT_NOT_SUCCESSFUL");
        }

        return StageRecoveryResult.Completed();
    }

    private async Task<StageRecoveryResult>
        CheckTestAsync(
            StageRecoveryRequest request,
            CancellationToken cancellationToken)
    {
        StageRecoveryResult report =
            await CheckReportAsync<TestExecutionReport>(
                request,
                ArtifactType.TestReport,
                value => value.IsSuccess,
                cancellationToken);

        if (!report.IsCompleted)
        {
            return report;
        }

        return await CheckArtifactAsync(
            request,
            ArtifactType.TestResults,
            artifactOptions.MaxArtifactBytes,
            cancellationToken);
    }

    private Task<ArtifactReadResult> ReadAsync(
        StageRecoveryRequest request,
        ArtifactType artifactType,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        return artifactReader.ReadAsync(
            new ArtifactReadRequest
            {
                JobId = request.JobId,
                RunId = request.RunId,
                ArtifactType = artifactType,
                MaxBytes = maxBytes,
                CorrelationId =
                    request.CorrelationId
            },
            cancellationToken);
    }

    private static StageRecoveryResult MapReadFailure(
        ArtifactReadResult read)
    {
        if (read.FailureKind ==
            ArtifactReadFailureKind.Cancelled)
        {
            return StageRecoveryResult.Cancelled(
                string.IsNullOrWhiteSpace(
                    read.ErrorCode)
                    ? "STAGE_RECOVERY_READ_CANCELLED"
                    : read.ErrorCode);
        }

        return StageRecoveryResult.InvalidEvidence(
            string.IsNullOrWhiteSpace(
                read.ErrorCode)
                ? "STAGE_RECOVERY_EVIDENCE_INVALID"
                : read.ErrorCode);
    }
}
