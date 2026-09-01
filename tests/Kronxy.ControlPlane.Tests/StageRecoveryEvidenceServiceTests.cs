using System.Text.Json;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Execution;
using Kronxy.Infrastructure.Execution;
using Xunit;
using Kronxy.Infrastructure.Artifacts;

namespace Kronxy.ControlPlane.Tests;

public sealed class StageRecoveryEvidenceServiceTests
{
    private static readonly Guid JobId =
        Guid.Parse(
            "11111111-1111-1111-1111-111111111111");

    private static readonly Guid RunId =
        Guid.Parse(
            "22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Missing_evidence_is_not_completed()
    {
        var reader = new FakeArtifactReader();

        var service =
            new StageRecoveryEvidenceService(
                reader,
                Options());

        StageRecoveryResult result =
            await service.CheckAsync(
                Request(
                    RecoveryStage.Context));

        Assert.Equal(
            StageRecoveryStatus.NotCompleted,
            result.Status);

        Assert.False(
            result.IsCompleted);
    }

    [Fact]
    public async Task Context_artifact_completes_stage()
    {
        var reader = new FakeArtifactReader
        {
            Handler = request =>
                Success(
                    request,
                    "context"u8.ToArray())
        };

        var service =
            new StageRecoveryEvidenceService(
                reader,
                Options());

        StageRecoveryResult result =
            await service.CheckAsync(
                Request(
                    RecoveryStage.Context));

        Assert.True(
            result.IsCompleted);

        Assert.Equal(
            ArtifactType.ContextPackage,
            Assert.Single(
                reader.Requests)
                .ArtifactType);
    }

    [Fact]
    public async Task Planning_artifact_completes_stage()
    {
        var reader = new FakeArtifactReader
        {
            Handler = request =>
                Success(
                    request,
                    "{}"u8.ToArray())
        };

        var service =
            new StageRecoveryEvidenceService(
                reader,
                Options());

        StageRecoveryResult result =
            await service.CheckAsync(
                Request(
                    RecoveryStage.Planning));

        Assert.True(
            result.IsCompleted);

        Assert.Equal(
            ArtifactType.AiResponse,
            Assert.Single(
                reader.Requests)
                .ArtifactType);
    }

    [Theory]
    [InlineData(
        RecoveryStage.Restore,
        ArtifactType.RestoreReport)]
    [InlineData(
        RecoveryStage.Build,
        ArtifactType.BuildReport)]
    public async Task Successful_execution_report_completes_stage(
        RecoveryStage stage,
        ArtifactType artifactType)
    {
        var reader = new FakeArtifactReader
        {
            Handler = request =>
            {
                byte[] content =
                    stage == RecoveryStage.Restore
                        ? JsonSerializer
                            .SerializeToUtf8Bytes(
                                SuccessfulRestoreReport())
                        : JsonSerializer
                            .SerializeToUtf8Bytes(
                                SuccessfulBuildReport());

                return Success(
                    request,
                    content);
            }
        };

        var service =
            new StageRecoveryEvidenceService(
                reader,
                Options());

        StageRecoveryResult result =
            await service.CheckAsync(
                Request(stage));

        Assert.True(
            result.IsCompleted);

        Assert.Equal(
            artifactType,
            Assert.Single(
                reader.Requests)
                .ArtifactType);
    }

    [Fact]
    public async Task Non_successful_build_report_is_invalid()
    {
        var reader = new FakeArtifactReader
        {
            Handler = request =>
                Success(
                    request,
                    JsonSerializer
                        .SerializeToUtf8Bytes(
                            new BuildExecutionReport(
                                JobId,
                                RunId,
                                "Kronxy.sln",
                                ToolExecutionOutcome
                                    .NonZeroExitCode,
                                1,
                                "BUILD_FAILED",
                                DateTime.UtcNow,
                                DateTime.UtcNow,
                                TimeSpan.Zero)))
        };

        var service =
            new StageRecoveryEvidenceService(
                reader,
                Options());

        StageRecoveryResult result =
            await service.CheckAsync(
                Request(
                    RecoveryStage.Build));

        Assert.Equal(
            StageRecoveryStatus.InvalidEvidence,
            result.Status);

        Assert.Equal(
            "STAGE_RECOVERY_REPORT_NOT_SUCCESSFUL",
            result.ErrorCode);
    }

    [Fact]
    public async Task Invalid_report_json_is_invalid()
    {
        var reader = new FakeArtifactReader
        {
            Handler = request =>
                Success(
                    request,
                    "not-json"u8.ToArray())
        };

        var service =
            new StageRecoveryEvidenceService(
                reader,
                Options());

        StageRecoveryResult result =
            await service.CheckAsync(
                Request(
                    RecoveryStage.Restore));

        Assert.Equal(
            StageRecoveryStatus.InvalidEvidence,
            result.Status);

        Assert.Equal(
            "STAGE_RECOVERY_REPORT_INVALID_JSON",
            result.ErrorCode);
    }

    [Fact]
    public async Task Test_requires_report_and_results()
    {
        var reader = new FakeArtifactReader
        {
            Handler = request =>
            {
                if (request.ArtifactType ==
                    ArtifactType.TestReport)
                {
                    return Success(
                        request,
                        JsonSerializer
                            .SerializeToUtf8Bytes(
                                SuccessfulTestReport()));
                }

                return ArtifactReadResult.Failure(
                    ArtifactReadFailureKind.NotFound,
                    "TEST_RESULTS_NOT_FOUND");
            }
        };

        var service =
            new StageRecoveryEvidenceService(
                reader,
                Options());

        StageRecoveryResult result =
            await service.CheckAsync(
                Request(
                    RecoveryStage.Test));

        Assert.Equal(
            StageRecoveryStatus.NotCompleted,
            result.Status);

        Assert.Equal(
            2,
            reader.Requests.Count);
    }

    [Fact]
    public async Task Test_report_and_results_complete_stage()
    {
        var reader = new FakeArtifactReader
        {
            Handler = request =>
                request.ArtifactType ==
                ArtifactType.TestReport
                    ? Success(
                        request,
                        JsonSerializer
                            .SerializeToUtf8Bytes(
                                SuccessfulTestReport()))
                    : Success(
                        request,
                        "<trx />"u8.ToArray())
        };

        var service =
            new StageRecoveryEvidenceService(
                reader,
                Options());

        StageRecoveryResult result =
            await service.CheckAsync(
                Request(
                    RecoveryStage.Test));

        Assert.True(
            result.IsCompleted);

        Assert.Equal(
            2,
            reader.Requests.Count);
    }

    [Fact]
    public async Task Corrupt_reader_evidence_fails_closed()
    {
        var reader = new FakeArtifactReader
        {
            Handler = _ =>
                ArtifactReadResult.Failure(
                    ArtifactReadFailureKind
                        .IntegrityFailure,
                    "ARTIFACT_HASH_MISMATCH")
        };

        var service =
            new StageRecoveryEvidenceService(
                reader,
                Options());

        StageRecoveryResult result =
            await service.CheckAsync(
                Request(
                    RecoveryStage.Context));

        Assert.Equal(
            StageRecoveryStatus.InvalidEvidence,
            result.Status);

        Assert.Equal(
            "ARTIFACT_HASH_MISMATCH",
            result.ErrorCode);
    }

    private static StageRecoveryRequest Request(
        RecoveryStage stage) =>
        new()
        {
            JobId = JobId,
            RunId = RunId,
            Stage = stage,
            CorrelationId = "recovery-test"
        };

    private static RestoreExecutionReport
        SuccessfulRestoreReport() =>
        new(
            JobId,
            RunId,
            "Kronxy.sln",
            ToolExecutionOutcome.Completed,
            0,
            string.Empty,
            DateTime.UtcNow,
            DateTime.UtcNow,
            TimeSpan.Zero);

    private static BuildExecutionReport
        SuccessfulBuildReport() =>
        new(
            JobId,
            RunId,
            "Kronxy.sln",
            ToolExecutionOutcome.Completed,
            0,
            string.Empty,
            DateTime.UtcNow,
            DateTime.UtcNow,
            TimeSpan.Zero);

    private static TestExecutionReport
        SuccessfulTestReport() =>
        new(
            JobId,
            RunId,
            "Kronxy.sln",
            ToolExecutionOutcome.Completed,
            0,
            string.Empty,
            DateTime.UtcNow,
            DateTime.UtcNow,
            TimeSpan.Zero);

    private static ArtifactReadResult Success(
        ArtifactReadRequest request,
        byte[] content) =>
        ArtifactReadResult.Success(
            new ArtifactRecord
            {
                ArtifactId = Guid.NewGuid(),
                JobId = request.JobId,
                RunId = request.RunId,
                ArtifactType =
                    request.ArtifactType,
                RelativePath =
                    $"test/{request.ArtifactType}.dat",
                Sha256 = new string('a', 64),
                SizeBytes = content.Length,
                CreatedAtUtc =
                    DateTimeOffset.UtcNow,
                CorrelationId =
                    request.CorrelationId
            },
            content);

    [Fact]
    public async Task Context_uses_configured_artifact_limit()
    {
        var reader =
            new FakeArtifactReader
            {
                Handler =
                    _ =>
                        ArtifactReadResult.Failure(
                            ArtifactReadFailureKind.NotFound,
                            "TEST_NOT_FOUND")
            };

        ArtifactStoreOptions options =
            Options() with
            {
                MaxArtifactBytes = 7_654_321
            };

        var service =
            new StageRecoveryEvidenceService(
                reader,
                options);

        StageRecoveryResult result =
            await service.CheckAsync(
                Request(
                    RecoveryStage.Context));

        Assert.Equal(
            StageRecoveryStatus.NotCompleted,
            result.Status);

        Assert.Equal(
            7_654_321,
            Assert.Single(
                reader.Requests)
                .MaxBytes);
    }

    [Fact]
    public async Task Planning_uses_configured_artifact_limit()
    {
        var reader =
            new FakeArtifactReader
            {
                Handler =
                    _ =>
                        ArtifactReadResult.Failure(
                            ArtifactReadFailureKind.NotFound,
                            "TEST_NOT_FOUND")
            };

        ArtifactStoreOptions options =
            Options() with
            {
                MaxArtifactBytes = 8_123_456
            };

        var service =
            new StageRecoveryEvidenceService(
                reader,
                options);

        await service.CheckAsync(
            Request(
                RecoveryStage.Planning));

        Assert.Equal(
            8_123_456,
            Assert.Single(
                reader.Requests)
                .MaxBytes);
    }

    [Fact]
    public async Task Test_report_uses_report_limit_and_results_use_artifact_limit()
    {
        var reader =
            new FakeArtifactReader
            {
                Handler =
                    request =>
                    {
                        if (request.ArtifactType ==
                            ArtifactType.TestReport)
                        {
                            return Success(
                                request,
                                System.Text.Json.JsonSerializer
                                    .SerializeToUtf8Bytes(
                                        SuccessfulTestReport()));
                        }

                        return ArtifactReadResult.Failure(
                            ArtifactReadFailureKind.NotFound,
                            "TEST_NOT_FOUND");
                    }
            };

        ArtifactStoreOptions options =
            Options() with
            {
                MaxArtifactBytes = 9_234_567
            };

        var service =
            new StageRecoveryEvidenceService(
                reader,
                options);

        StageRecoveryResult result =
            await service.CheckAsync(
                Request(
                    RecoveryStage.Test));

        Assert.Equal(
            StageRecoveryStatus.NotCompleted,
            result.Status);

        Assert.Equal(
            2,
            reader.Requests.Count);

        Assert.Equal(
            ArtifactType.TestReport,
            reader.Requests[0].ArtifactType);

        Assert.Equal(
            1_048_576,
            reader.Requests[0].MaxBytes);

        Assert.Equal(
            ArtifactType.TestResults,
            reader.Requests[1].ArtifactType);

        Assert.Equal(
            9_234_567,
            reader.Requests[1].MaxBytes);
    }

    private static ArtifactStoreOptions Options()
    {
        return new ArtifactStoreOptions
        {
            RootPath = "/tmp/kronxy-artifacts-tests",
            MaxArtifactBytes = 16_777_216
        };
    }

    private sealed class FakeArtifactReader :
        IArtifactReader
    {
        public Func<
            ArtifactReadRequest,
            ArtifactReadResult>? Handler
        {
            get;
            init;
        }

        public List<ArtifactReadRequest> Requests
        {
            get;
        } = [];

        public Task<ArtifactReadResult> ReadAsync(
            ArtifactReadRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            Requests.Add(request);

            ArtifactReadResult result =
                Handler?.Invoke(request) ??
                ArtifactReadResult.Failure(
                    ArtifactReadFailureKind.NotFound,
                    "TEST_NOT_FOUND");

            return Task.FromResult(result);
        }
    }
}
