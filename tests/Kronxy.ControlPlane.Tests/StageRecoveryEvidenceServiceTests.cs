using System.Text.Json;
using Kronxy.Application.AI;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Execution;
using Kronxy.Infrastructure.Execution;
using Xunit;
using Kronxy.Infrastructure.Artifacts;
using Kronxy.Infrastructure.AI;

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
    public async Task Planning_artifacts_complete_stage()
    {
        var reader = new FakeArtifactReader
        {
            Handler = request =>
                request.ArtifactType ==
                    ArtifactType.PlanningPlan
                    ? Success(
                        request,
                        JsonSerializer
                            .SerializeToUtf8Bytes(
                                ValidPlan()))
                    : Success(
                        request,
                        "{}"u8.ToArray())
        };

        var service =
            new StageRecoveryEvidenceService(
                reader,
                Options(),
                priorityPathSelector:
                    new FakePriorityPathSelector());

        StageRecoveryResult result =
            await service.CheckAsync(
                Request(
                    RecoveryStage.Planning));

        Assert.True(
            result.IsCompleted);

        Assert.Collection(
            reader.Requests,
            request => Assert.Equal(
                ArtifactType.AiResponse,
                request.ArtifactType),
            request => Assert.Equal(
                ArtifactType.PlanningPlan,
                request.ArtifactType),
            request => Assert.Equal(
                ArtifactType.ContextPackage,
                request.ArtifactType));
    }
    [Fact]
    public async Task Planning_without_top_five_overlap_is_invalid_evidence()
    {
        var reader = new FakeArtifactReader
        {
            Handler = request =>
                request.ArtifactType == ArtifactType.PlanningPlan
                    ? Success(
                        request,
                        JsonSerializer.SerializeToUtf8Bytes(
                            ValidPlan()))
                    : Success(
                        request,
                        "context"u8.ToArray())
        };

        var service =
            new StageRecoveryEvidenceService(
                reader,
                Options(),
                priorityPathSelector:
                    new FakePriorityPathSelector
                    {
                        Result = ["src/unrelated.cs"]
                    });

        StageRecoveryResult result =
            await service.CheckAsync(
                Request(RecoveryStage.Planning));

        Assert.Equal(
            StageRecoveryStatus.InvalidEvidence,
            result.Status);
        Assert.Equal(
            "STAGE_RECOVERY_PLANNING_COHERENCE_INVALID",
            result.ErrorCode);
    }

    [Fact]
    public async Task Planning_with_missing_context_fails_closed()
    {
        var reader = new FakeArtifactReader
        {
            Handler = request =>
                request.ArtifactType == ArtifactType.ContextPackage
                    ? ArtifactReadResult.Failure(
                        ArtifactReadFailureKind.NotFound,
                        "CONTEXT_PACKAGE_NOT_FOUND")
                    : request.ArtifactType == ArtifactType.PlanningPlan
                        ? Success(
                            request,
                            JsonSerializer.SerializeToUtf8Bytes(
                                ValidPlan()))
                        : Success(
                            request,
                            "{}"u8.ToArray())
        };

        var service =
            new StageRecoveryEvidenceService(
                reader,
                Options(),
                priorityPathSelector:
                    new FakePriorityPathSelector());

        StageRecoveryResult result =
            await service.CheckAsync(
                Request(RecoveryStage.Planning));

        Assert.Equal(
            StageRecoveryStatus.InvalidEvidence,
            result.Status);
        Assert.Equal(
            "CONTEXT_PACKAGE_NOT_FOUND",
            result.ErrorCode);
    }

    [Fact]
    public async Task Planning_plan_unrelated_to_job_request_is_invalid_evidence()
    {
        PlannerPlan unrelated =
            ValidPlan() with
            {
                Objective =
                    "Create a new project using the projects API.",
                FilesToInspect =
                [
                    "src/Kronxy.Api/Controllers/Projects/ProjectsController.cs"
                ],
                CandidateFilesToModify =
                [
                    "src/Kronxy.Api/Controllers/Projects/ProjectsController.cs"
                ]
            };

        var reader =
            new FakeArtifactReader
            {
                Handler = request =>
                    request.ArtifactType ==
                        ArtifactType.PlanningPlan
                        ? Success(
                            request,
                            JsonSerializer
                                .SerializeToUtf8Bytes(
                                    unrelated))
                        : Success(
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

        Assert.Equal(
            StageRecoveryStatus.InvalidEvidence,
            result.Status);

        Assert.Equal(
            "STAGE_RECOVERY_PLANNING_POLICY_INVALID",
            result.ErrorCode);

        Assert.Null(
            result.PlannerPlan);

        Assert.Equal(
            2,
            reader.Requests.Count);
    }

    [Fact]
    public async Task Planning_plan_without_authoritative_job_request_is_invalid_evidence()
    {
        var reader =
            new FakeArtifactReader
            {
                Handler = request =>
                    request.ArtifactType ==
                        ArtifactType.PlanningPlan
                        ? Success(
                            request,
                            JsonSerializer
                                .SerializeToUtf8Bytes(
                                    ValidPlan()))
                        : Success(
                            request,
                            "{}"u8.ToArray())
            };

        var service =
            new StageRecoveryEvidenceService(
                reader,
                Options());

        StageRecoveryRequest request =
            Request(
                RecoveryStage.Planning)
            with
            {
                JobRequest =
                    string.Empty
            };

        StageRecoveryResult result =
            await service.CheckAsync(
                request);

        Assert.Equal(
            StageRecoveryStatus.InvalidEvidence,
            result.Status);

        Assert.Equal(
            "STAGE_RECOVERY_PLANNING_POLICY_INVALID",
            result.ErrorCode);

        Assert.Null(
            result.PlannerPlan);
    }

    [Fact]
    public async Task Planning_without_plan_is_not_completed()
    {
        var reader = new FakeArtifactReader
        {
            Handler = request =>
                request.ArtifactType ==
                    ArtifactType.AiResponse
                    ? Success(
                        request,
                        "{}"u8.ToArray())
                    : ArtifactReadResult.Failure(
                        ArtifactReadFailureKind.NotFound,
                        "PLANNING_PLAN_NOT_FOUND")
        };

        var service =
            new StageRecoveryEvidenceService(
                reader,
                Options());

        StageRecoveryResult result =
            await service.CheckAsync(
                Request(
                    RecoveryStage.Planning));

        Assert.Equal(
            StageRecoveryStatus.NotCompleted,
            result.Status);

        Assert.Equal(
            2,
            reader.Requests.Count);
    }

    [Fact]
    public async Task Invalid_planning_plan_is_invalid_evidence()
    {
        var reader = new FakeArtifactReader
        {
            Handler = request =>
                request.ArtifactType ==
                    ArtifactType.PlanningPlan
                    ? Success(
                        request,
                        "not-json"u8.ToArray())
                    : Success(
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

        Assert.Equal(
            StageRecoveryStatus.InvalidEvidence,
            result.Status);

        Assert.Equal(
            "STAGE_RECOVERY_REPORT_INVALID_JSON",
            result.ErrorCode);
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
                if (request.ArtifactType is
                    ArtifactType.BuildCorrectionReport or
                    ArtifactType.BuildCorrectionRetryReport)
                {
                    return ArtifactReadResult.Failure(
                        ArtifactReadFailureKind.NotFound,
                        "TEST_NOT_FOUND");
                }

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

        Assert.Equal(artifactType, reader.Requests[^1].ArtifactType);
        Assert.Equal(stage == RecoveryStage.Build ? 3 : 1, reader.Requests.Count);
    }

    [Fact]
    public async Task Failed_original_build_is_recoverable_with_compiler_output()
    {
        var reader = new FakeArtifactReader
        {
            Handler = request => request.ArtifactType switch
            {
                ArtifactType.BuildCorrectionReport or
                    ArtifactType.BuildCorrectionRetryReport =>
                    ArtifactReadResult.Failure(
                        ArtifactReadFailureKind.NotFound,
                        "TEST_NOT_FOUND"),
                ArtifactType.BuildStandardOutput =>
                    Success(request, "error CS0246"u8.ToArray()),
                _ => Success(
                    request,
                    JsonSerializer.SerializeToUtf8Bytes(
                        new BuildExecutionReport(
                            JobId,
                            RunId,
                            "Kronxy.sln",
                            ToolExecutionOutcome.NonZeroExitCode,
                            1,
                            "BUILD_FAILED",
                            DateTime.UtcNow,
                            DateTime.UtcNow,
                            TimeSpan.Zero)))
            }
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
            StageRecoveryStatus.FailedExecution,
            result.Status);
        Assert.Equal("error CS0246", result.BuildStandardOutput);
        Assert.NotNull(result.BuildReport);
    }

    [Fact]
    public async Task Failed_corrected_build_is_recoverable_as_exhaustion_evidence()
    {
        var reader = new FakeArtifactReader
        {
            Handler = request => request.ArtifactType switch
            {
                ArtifactType.BuildCorrectionStandardOutput =>
                    Success(request, "error CS0246"u8.ToArray()),
                ArtifactType.BuildCorrectionReport => Success(
                    request,
                    JsonSerializer.SerializeToUtf8Bytes(
                        new BuildExecutionReport(
                            JobId,
                            RunId,
                            "Kronxy.sln",
                            ToolExecutionOutcome.NonZeroExitCode,
                            1,
                            "TOOL_NONZERO_EXIT",
                            DateTime.UtcNow,
                            DateTime.UtcNow,
                            TimeSpan.Zero))),
                _ => throw new InvalidOperationException(
                    $"Unexpected artifact {request.ArtifactType}.")
            }
        };
        var service = new StageRecoveryEvidenceService(reader, Options());

        StageRecoveryResult result = await service.CheckAsync(
            Request(RecoveryStage.BuildCorrectionFailure));

        Assert.Equal(StageRecoveryStatus.FailedExecution, result.Status);
        Assert.Equal("error CS0246", result.BuildStandardOutput);
        Assert.NotNull(result.BuildReport);
        Assert.False(result.BuildReport.IsSuccess);
    }

    [Fact]
    public async Task Successful_corrected_build_is_not_failure_evidence()
    {
        var reader = new FakeArtifactReader
        {
            Handler = request => request.ArtifactType switch
            {
                ArtifactType.BuildCorrectionStandardOutput =>
                    Success(request, "Build succeeded."u8.ToArray()),
                ArtifactType.BuildCorrectionReport => Success(
                    request,
                    JsonSerializer.SerializeToUtf8Bytes(
                        SuccessfulBuildReport())),
                _ => throw new InvalidOperationException(
                    $"Unexpected artifact {request.ArtifactType}.")
            }
        };
        var service = new StageRecoveryEvidenceService(reader, Options());

        StageRecoveryResult result = await service.CheckAsync(
            Request(RecoveryStage.BuildCorrectionFailure));

        Assert.True(result.IsCompleted);
    }

    [Fact]
    public async Task Governed_human_build_failure_accepts_matching_legacy_correlation()
    {
        var reader = new FakeArtifactReader
        {
            Handler = request => request.ArtifactType switch
            {
                ArtifactType.BuildGovernedHumanCorrectionReport =>
                    ArtifactReadResult.Failure(
                        ArtifactReadFailureKind.NotFound,
                        "TEST_NOT_FOUND"),
                ArtifactType.BuildHumanReviewCorrectionReport => Success(
                    request,
                    JsonSerializer.SerializeToUtf8Bytes(
                        new BuildExecutionReport(
                            JobId,
                            RunId,
                            "Kronxy.sln",
                            ToolExecutionOutcome.NonZeroExitCode,
                            1,
                            "TOOL_NONZERO_EXIT",
                            DateTime.UtcNow,
                            DateTime.UtcNow,
                            TimeSpan.Zero))),
                ArtifactType.BuildHumanReviewCorrectionStandardOutput =>
                    Success(request, "error CS0246"u8.ToArray()),
                _ => throw new InvalidOperationException(
                    $"Unexpected artifact {request.ArtifactType}.")
            }
        };

        StageRecoveryResult result = await new StageRecoveryEvidenceService(
            reader, Options()).CheckAsync(
                Request(RecoveryStage.BuildGovernedHumanCorrectionFailure));

        Assert.Equal(StageRecoveryStatus.FailedExecution, result.Status);
        Assert.Equal("error CS0246", result.BuildStandardOutput);
    }

    [Fact]
    public async Task Governed_human_build_failure_rejects_stale_legacy_correlation()
    {
        var reader = new FakeArtifactReader
        {
            Handler = request => request.ArtifactType switch
            {
                ArtifactType.BuildGovernedHumanCorrectionReport =>
                    ArtifactReadResult.Failure(
                        ArtifactReadFailureKind.NotFound,
                        "TEST_NOT_FOUND"),
                ArtifactType.BuildHumanReviewCorrectionReport => Success(
                    request,
                    JsonSerializer.SerializeToUtf8Bytes(
                        new BuildExecutionReport(
                            JobId,
                            RunId,
                            "Kronxy.sln",
                            ToolExecutionOutcome.NonZeroExitCode,
                            1,
                            "TOOL_NONZERO_EXIT",
                            DateTime.UtcNow,
                            DateTime.UtcNow,
                            TimeSpan.Zero)),
                    "older-correction"),
                _ => throw new InvalidOperationException(
                    $"Unexpected artifact {request.ArtifactType}.")
            }
        };

        StageRecoveryResult result = await new StageRecoveryEvidenceService(
            reader, Options()).CheckAsync(
                Request(RecoveryStage.BuildGovernedHumanCorrectionFailure));

        Assert.Equal(StageRecoveryStatus.NotCompleted, result.Status);
        Assert.DoesNotContain(
            reader.Requests,
            request => request.ArtifactType ==
                ArtifactType.BuildHumanReviewCorrectionStandardOutput);
    }

    [Fact]
    public async Task Successful_correction_build_is_preferred_over_original_failure()
    {
        var reader = new FakeArtifactReader
        {
            Handler = request =>
                request.ArtifactType == ArtifactType.BuildCorrectionReport
                    ? Success(
                        request,
                        JsonSerializer.SerializeToUtf8Bytes(
                            SuccessfulBuildReport()))
                    : request.ArtifactType == ArtifactType.BuildCorrectionRetryReport
                        ? ArtifactReadResult.Failure(
                            ArtifactReadFailureKind.NotFound, "TEST_NOT_FOUND")
                        : throw new InvalidOperationException(
                        "Original build evidence must not be read.")
        };

        var service = new StageRecoveryEvidenceService(reader, Options());

        StageRecoveryResult result = await service.CheckAsync(
            Request(RecoveryStage.Build));

        Assert.True(result.IsCompleted);
        Assert.Equal(
            ArtifactType.BuildCorrectionReport,
            reader.Requests[^1].ArtifactType);
    }

    [Fact]
    public async Task Developer_recovery_merges_separate_build_correction_without_overwriting_original()
    {
        const string originalContent = "class NewType {}";
        string hash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(originalContent)))
            .ToLowerInvariant();
        byte[] response = JsonSerializer.SerializeToUtf8Bytes(
            new AiResponse
            {
                Status = AiOperationStatus.Success,
                Content = "{}",
                Provider = "Fake",
                LogicalModel = "CodingQuality",
                PhysicalModel = "quality"
            });
        byte[] original =
            """
            {
              "summary":"Create a file.",
              "changes":[{
                "operation":"CreateFile",
                "relativePath":"src/new.cs",
                "intent":"Create it.",
                "content":"class NewType {}",
                "expectedContentSha256":""
              }],
              "assumptions":[],"risks":[]
            }
            """u8.ToArray();
        byte[] correction = System.Text.Encoding.UTF8.GetBytes(
            $$"""
            {
              "summary":"Fix the build.",
              "changes":[{
                "operation":"ReplaceFile",
                "relativePath":"src/new.cs",
                "intent":"Add the import.",
                "content":"using Missing.Namespace;\nclass NewType {}",
                "expectedContentSha256":"{{hash}}"
              }],
              "assumptions":[],"risks":[]
            }
            """);
        var reader = new FakeArtifactReader
        {
            Handler = request => request.ArtifactType switch
            {
                ArtifactType.DeveloperProposal => Success(request, original),
                ArtifactType.DeveloperResponse => Success(request, response),
                ArtifactType.DeveloperBuildCorrectionProposal =>
                    Success(request, correction),
                ArtifactType.DeveloperBuildCorrectionResponse =>
                    Success(request, response),
                _ => ArtifactReadResult.Failure(
                    ArtifactReadFailureKind.NotFound,
                    "TEST_NOT_FOUND")
            }
        };

        var service = new StageRecoveryEvidenceService(reader, Options());

        StageRecoveryResult result = await service.CheckAsync(
            Request(RecoveryStage.Developer));

        Assert.True(result.IsCompleted);
        ValidatedDeveloperChange change = Assert.Single(
            result.DeveloperProposal!.Changes);
        Assert.Equal(DeveloperChangeOperationType.CreateFile, change.Operation);
        Assert.Contains("using Missing.Namespace", change.Content);
        Assert.Equal(
            [
                ArtifactType.DeveloperProposal,
                ArtifactType.DeveloperResponse,
                ArtifactType.DeveloperBuildCorrectionRetryProposal,
                ArtifactType.DeveloperBuildCorrectionRetryResponse,
                ArtifactType.DeveloperBuildCorrectionProposal,
                ArtifactType.DeveloperBuildCorrectionResponse,
                ArtifactType.HumanReviewCorrectionEvidence,
                ArtifactType.DeveloperHumanReviewCorrectionProposal,
                ArtifactType.DeveloperHumanReviewCorrectionResponse
            ],
            reader.Requests.Select(item => item.ArtifactType));
    }

    [Fact]
    public async Task Effective_developer_proposal_uses_original_when_no_correction_exists()
    {
        (byte[] response, byte[] original, _, _) = EffectiveProposalArtifacts();
        var reader = new FakeArtifactReader
        {
            Handler = request => request.ArtifactType switch
            {
                ArtifactType.DeveloperProposal => Success(request, original),
                ArtifactType.DeveloperResponse => Success(request, response),
                _ => ArtifactReadResult.Failure(
                    ArtifactReadFailureKind.NotFound, "TEST_NOT_FOUND")
            }
        };

        StageRecoveryResult result = await new StageRecoveryEvidenceService(
            reader, Options()).CheckAsync(
                Request(RecoveryStage.EffectiveDeveloperProposal));

        Assert.True(result.IsCompleted);
        Assert.Equal(DeveloperProposalLineage.Original,
            result.DeveloperProposalLineage);
        Assert.Equal("class Existing {}",
            Assert.Single(result.DeveloperProposal!.Changes).Content);
    }

    [Fact]
    public async Task Effective_developer_proposal_uses_build_correction_when_present()
    {
        (byte[] response, byte[] original, byte[] build, _) =
            EffectiveProposalArtifacts();
        var reader = new FakeArtifactReader
        {
            Handler = request => request.ArtifactType switch
            {
                ArtifactType.DeveloperProposal => Success(request, original),
                ArtifactType.DeveloperResponse => Success(request, response),
                ArtifactType.DeveloperBuildCorrectionProposal =>
                    Success(request, build),
                ArtifactType.DeveloperBuildCorrectionResponse =>
                    Success(request, response),
                _ => ArtifactReadResult.Failure(
                    ArtifactReadFailureKind.NotFound, "TEST_NOT_FOUND")
            }
        };

        StageRecoveryResult result = await new StageRecoveryEvidenceService(
            reader, Options()).CheckAsync(
                Request(RecoveryStage.EffectiveDeveloperProposal));

        Assert.True(result.IsCompleted);
        Assert.Equal(DeveloperProposalLineage.BuildCorrection,
            result.DeveloperProposalLineage);
        Assert.Contains("BuildFixed",
            Assert.Single(result.DeveloperProposal!.Changes).Content);
    }

    [Fact]
    public async Task Developer_correction_with_mismatched_lineage_fails_closed()
    {
        (byte[] response, _, byte[] build, _) =
            EffectiveProposalArtifacts();
        var reader = new FakeArtifactReader
        {
            Handler = request => request.ArtifactType switch
            {
                ArtifactType.DeveloperBuildCorrectionProposal =>
                    Success(request, build, "proposal-correlation"),
                ArtifactType.DeveloperBuildCorrectionResponse =>
                    Success(request, response, "response-correlation"),
                _ => ArtifactReadResult.Failure(
                    ArtifactReadFailureKind.NotFound, "TEST_NOT_FOUND")
            }
        };

        StageRecoveryResult result = await new StageRecoveryEvidenceService(
            reader, Options()).CheckAsync(
                Request(RecoveryStage.DeveloperBuildCorrection));

        Assert.Equal(StageRecoveryStatus.InvalidEvidence, result.Status);
        Assert.Equal(
            "STAGE_RECOVERY_DEVELOPER_LINEAGE_MISMATCH",
            result.ErrorCode);
    }

    [Fact]
    public async Task Effective_developer_proposal_uses_human_correction_when_present()
    {
        (byte[] response, byte[] original, _, byte[] human) =
            EffectiveProposalArtifacts();
        byte[] evidence = JsonSerializer.SerializeToUtf8Bytes(
            new HumanReviewCorrectionEvidence
            {
                JobId = JobId,
                RunId = RunId,
                Decision = HumanReviewDecision.ChangesRequired,
                RequiredCorrections =
                [
                    new HumanReviewRequiredCorrection
                    {
                        RelativePath = "src/existing.cs",
                        Instruction = "Apply the itemized human correction."
                    }
                ],
                RecordedAtUtc = DateTimeOffset.UtcNow
            });
        var reader = new FakeArtifactReader
        {
            Handler = request => request.ArtifactType switch
            {
                ArtifactType.DeveloperProposal => Success(request, original),
                ArtifactType.DeveloperResponse => Success(request, response),
                ArtifactType.HumanReviewCorrectionEvidence =>
                    Success(request, evidence),
                ArtifactType.DeveloperHumanReviewCorrectionProposal =>
                    Success(request, human),
                ArtifactType.DeveloperHumanReviewCorrectionResponse =>
                    Success(request, response),
                _ => ArtifactReadResult.Failure(
                    ArtifactReadFailureKind.NotFound, "TEST_NOT_FOUND")
            }
        };

        StageRecoveryResult result = await new StageRecoveryEvidenceService(
            reader, Options()).CheckAsync(
                Request(RecoveryStage.EffectiveDeveloperProposal));

        Assert.True(result.IsCompleted);
        Assert.Equal(DeveloperProposalLineage.HumanReviewCorrection,
            result.DeveloperProposalLineage);
        Assert.Contains("HumanFixed",
            Assert.Single(result.DeveloperProposal!.Changes).Content);
    }

    [Fact]
    public async Task Effective_developer_proposal_fails_closed_when_human_proposal_is_missing()
    {
        (byte[] response, byte[] original, _, _) = EffectiveProposalArtifacts();
        byte[] evidence = JsonSerializer.SerializeToUtf8Bytes(
            new HumanReviewCorrectionEvidence
            {
                JobId = JobId,
                RunId = RunId,
                Decision = HumanReviewDecision.ChangesRequired,
                RequiredCorrections =
                [
                    new HumanReviewRequiredCorrection
                    {
                        RelativePath = "src/existing.cs",
                        Instruction = "The correction proposal is mandatory."
                    }
                ],
                RecordedAtUtc = DateTimeOffset.UtcNow
            });
        var reader = new FakeArtifactReader
        {
            Handler = request => request.ArtifactType switch
            {
                ArtifactType.DeveloperProposal => Success(request, original),
                ArtifactType.DeveloperResponse => Success(request, response),
                ArtifactType.HumanReviewCorrectionEvidence =>
                    Success(request, evidence),
                _ => ArtifactReadResult.Failure(
                    ArtifactReadFailureKind.NotFound, "TEST_NOT_FOUND")
            }
        };

        StageRecoveryResult result = await new StageRecoveryEvidenceService(
            reader, Options()).CheckAsync(
                Request(RecoveryStage.EffectiveDeveloperProposal));

        Assert.Equal(StageRecoveryStatus.InvalidEvidence, result.Status);
        Assert.Equal("STAGE_RECOVERY_HUMAN_CORRECTION_PROPOSAL_MISSING",
            result.ErrorCode);
    }

    private static (byte[] Response, byte[] Original, byte[] Build, byte[] Human)
        EffectiveProposalArtifacts()
    {
        byte[] response = JsonSerializer.SerializeToUtf8Bytes(new AiResponse
        {
            Status = AiOperationStatus.Success,
            Content = "{}",
            Provider = "Fake",
            LogicalModel = "CodingQuality",
            PhysicalModel = "quality"
        });
        const string originalContent = "class Existing {}";
        string originalHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(originalContent)))
            .ToLowerInvariant();
        byte[] original = System.Text.Encoding.UTF8.GetBytes(
            "{\"summary\":\"Original\",\"changes\":[{\"operation\":\"CreateFile\",\"relativePath\":\"src/existing.cs\",\"intent\":\"Create\",\"content\":\"class Existing {}\",\"expectedContentSha256\":\"\"}],\"assumptions\":[],\"risks\":[]}");
        byte[] build = System.Text.Encoding.UTF8.GetBytes(
            $"{{\"summary\":\"Build\",\"changes\":[{{\"operation\":\"ReplaceFile\",\"relativePath\":\"src/existing.cs\",\"intent\":\"Fix build\",\"content\":\"class Existing {{ int BuildFixed; }}\",\"expectedContentSha256\":\"{originalHash}\"}}],\"assumptions\":[],\"risks\":[]}}");
        byte[] human = System.Text.Encoding.UTF8.GetBytes(
            $"{{\"summary\":\"Human\",\"changes\":[{{\"operation\":\"ReplaceFile\",\"relativePath\":\"src/existing.cs\",\"intent\":\"Human finding\",\"content\":\"class Existing {{ int HumanFixed; }}\",\"expectedContentSha256\":\"{originalHash}\"}}],\"assumptions\":[],\"risks\":[]}}");
        return (response, original, build, human);
    }

    [Fact]
    public async Task Human_review_correction_requires_replace_of_existing_proposal_file()
    {
        byte[] response = JsonSerializer.SerializeToUtf8Bytes(new AiResponse
        {
            Status = AiOperationStatus.Success, Content = "{}", Provider = "Fake",
            LogicalModel = "CodingQuality", PhysicalModel = "quality"
        });
        const string content = "class Existing {}";
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
        byte[] original = System.Text.Encoding.UTF8.GetBytes(
            """
            {"summary":"Create","changes":[{"operation":"CreateFile","relativePath":"src/existing.cs","intent":"Create","content":"class Existing {}","expectedContentSha256":""}],"assumptions":[],"risks":[]}
            """);
        byte[] invalidCorrection = System.Text.Encoding.UTF8.GetBytes(
            $$"""
            {"summary":"Human correction","changes":[{"operation":"CreateFile","relativePath":"src/existing.cs","intent":"Recreate","content":"class Existing { public int Value { get; } }","expectedContentSha256":"{{hash}}"}],"assumptions":[],"risks":[]}
            """);
        var reader = new FakeArtifactReader
        {
            Handler = request => request.ArtifactType switch
            {
                ArtifactType.DeveloperProposal => Success(request, original),
                ArtifactType.DeveloperResponse => Success(request, response),
                ArtifactType.DeveloperHumanReviewCorrectionProposal => Success(request, invalidCorrection),
                ArtifactType.DeveloperHumanReviewCorrectionResponse => Success(request, response),
                _ => ArtifactReadResult.Failure(ArtifactReadFailureKind.NotFound, "TEST_NOT_FOUND")
            }
        };

        StageRecoveryResult result = await new StageRecoveryEvidenceService(reader, Options())
            .CheckAsync(Request(RecoveryStage.DeveloperHumanReviewCorrection));

        Assert.Equal(StageRecoveryStatus.InvalidEvidence, result.Status);
    }

    [Fact]
    public async Task Human_review_evidence_rejects_unsafe_path()
    {
        var evidence = new HumanReviewCorrectionEvidence
        {
            JobId = JobId, RunId = RunId,
            Decision = HumanReviewDecision.ChangesRequired,
            RequiredCorrections =
            [
                new HumanReviewRequiredCorrection
                {
                    RelativePath = "../outside.cs",
                    Instruction = "Do not escape the allowlist."
                }
            ],
            RecordedAtUtc = DateTimeOffset.UtcNow
        };
        var reader = new FakeArtifactReader
        {
            Handler = request => Success(request, JsonSerializer.SerializeToUtf8Bytes(evidence))
        };

        StageRecoveryResult result = await new StageRecoveryEvidenceService(reader, Options())
            .CheckAsync(Request(RecoveryStage.HumanReviewCorrection));

        Assert.Equal(StageRecoveryStatus.InvalidEvidence, result.Status);
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
            JobRequest =
                "Recover validated planning evidence.",
            CorrelationId = "recovery-test"
        };

    private static PlannerPlan ValidPlan() =>
        new()
        {
            Objective = "Recover validated planning evidence.",
            FilesToInspect = ["src/a.cs"],
            CandidateFilesToModify = Array.Empty<string>(),
            Strategy = "Require both planning artifacts.",
            AcceptanceCriteria =
                Array.Empty<string>(),
            Risks = Array.Empty<string>(),
            ExpectedTests = Array.Empty<string>(),
            Assumptions = Array.Empty<string>(),
            Uncertainties = Array.Empty<string>()
        };

    private sealed class FakePriorityPathSelector :
        IPlanningPriorityPathSelector
    {
        public IReadOnlyList<string> Result { get; set; } =
            ["src/a.cs"];

        public IReadOnlyList<string> Select(
            ReadOnlyMemory<byte> packageContent,
            string jobRequest) =>
            Result;
    }

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
        Success(request, content, request.CorrelationId);

    private static ArtifactReadResult Success(
        ArtifactReadRequest request,
        byte[] content,
        string correlationId) =>
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
                    correlationId
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
