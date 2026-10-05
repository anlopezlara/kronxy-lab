using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kronxy.Application.AI;
using Kronxy.Infrastructure.AI;
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
    private readonly IDeveloperProposalPolicy developerPolicy;
    private readonly AiStructuredOutputValidator structuredValidator;
    private readonly IPlannerPlanPolicy plannerPlanPolicy;
    private readonly IPlanningPriorityPathSelector priorityPathSelector;

    public StageRecoveryEvidenceService(
        IArtifactReader artifactReader,
        ArtifactStoreOptions artifactOptions,
        IDeveloperProposalPolicy? developerPolicy = null,
        AiStructuredOutputValidator? structuredValidator = null,
        IPlannerPlanPolicy? plannerPlanPolicy = null,
        IPlanningPriorityPathSelector? priorityPathSelector = null)
    {
        this.artifactReader =
            artifactReader ??
            throw new ArgumentNullException(
                nameof(artifactReader));

        this.artifactOptions =
            artifactOptions ??
            throw new ArgumentNullException(
                nameof(artifactOptions));

        this.developerPolicy = developerPolicy ??
            new DeveloperProposalPolicy(
                new DeveloperChangePolicyOptions());
        this.structuredValidator = structuredValidator ??
            new AiStructuredOutputValidator();

        this.plannerPlanPolicy = plannerPlanPolicy ??
            new PlannerPlanPolicy();
        this.priorityPathSelector = priorityPathSelector ??
            new PlanningPriorityPathSelector();

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

                RecoveryStage.DevelopmentAnalysis =>
                    await CheckReportAsync<DevelopmentAnalysis>(
                        request,
                        ArtifactType.DevelopmentAnalysis,
                        analysis => analysis.JobId == request.JobId &&
                            analysis.RunId == request.RunId &&
                            analysis.AttemptCount == request.AttemptCount &&
                            !string.IsNullOrWhiteSpace(analysis.RequestIdentity) &&
                            !string.IsNullOrWhiteSpace(analysis.AnalysisVersion) &&
                            analysis.TargetSymbols is not null &&
                            analysis.ExistingDeclarations is not null &&
                            analysis.ImpactedLayers is not null &&
                            analysis.BreakingContracts is not null &&
                            analysis.Evidence is not null &&
                            analysis.FilesInspected is not null,
                        cancellationToken,
                        analysis => StageRecoveryResult.Completed(
                            developmentAnalysis: analysis)),

                RecoveryStage.Planning =>
                    await CheckPlanningAsync(
                        request,
                        cancellationToken),

                RecoveryStage.HumanReviewCorrection =>
                    await CheckHumanReviewCorrectionAsync(
                        request,
                        cancellationToken),

                RecoveryStage.GovernedHumanCorrection =>
                    await CheckGovernedHumanCorrectionAsync(
                        request,
                        cancellationToken),

                RecoveryStage.Developer =>
                    await CheckHumanDeveloperAsync(
                        request,
                        humanCorrectionOnly: false,
                        cancellationToken),

                RecoveryStage.DeveloperOriginal =>
                    await CheckDeveloperAsync(
                        request,
                        preferBuildCorrection: false,
                        correctionOnly: false,
                        cancellationToken),

                RecoveryStage.DeveloperBuildCorrection =>
                    await CheckDeveloperAsync(
                        request,
                        preferBuildCorrection: true,
                        correctionOnly: true,
                        cancellationToken),

                RecoveryStage.DeveloperBuildCorrectionRetry =>
                    await CheckDeveloperAsync(
                        request,
                        preferBuildCorrection: true,
                        correctionOnly: true,
                        cancellationToken),

                RecoveryStage.DeveloperHumanReviewCorrection =>
                    await CheckHumanDeveloperAsync(
                        request,
                        humanCorrectionOnly: true,
                        cancellationToken),

                RecoveryStage.EffectiveDeveloperProposal =>
                    await CheckEffectiveDeveloperProposalAsync(
                        request,
                        cancellationToken),

                RecoveryStage.ObservedChanges =>
                    await CheckObservedChangesAsync(
                        request,
                        cancellationToken),

                RecoveryStage.ObservedBuildCorrection =>
                    await CheckReportAsync<ObservedChangeManifest>(
                        request,
                        ArtifactType.ObservedBuildCorrectionManifest,
                        manifest => manifest.JobId == request.JobId &&
                            manifest.RunId == request.RunId &&
                            manifest.Entries is not null,
                        cancellationToken,
                        manifest => StageRecoveryResult.Completed(
                            observedChangeManifest: manifest)),

                RecoveryStage.ObservedBuildCorrectionRetry =>
                    await CheckObservedChangesArtifactAsync(
                        request,
                        ArtifactType.ObservedBuildCorrectionRetryManifest,
                        cancellationToken),

                RecoveryStage.ObservedHumanReviewCorrection =>
                    await CheckObservedChangesArtifactAsync(
                        request,
                        ArtifactType.ObservedHumanReviewCorrectionManifest,
                        cancellationToken),

                RecoveryStage.ObservedGovernedHumanCorrection =>
                    await CheckObservedChangesArtifactAsync(
                        request,
                        ArtifactType.ObservedGovernedHumanCorrectionManifest,
                        cancellationToken),

                RecoveryStage.Restore =>
                    await CheckReportAsync<RestoreExecutionReport>(
                        request,
                        ArtifactType.RestoreReport,
                        report => report.IsSuccess,
                        cancellationToken),

                RecoveryStage.Build =>
                    await CheckBuildAsync(
                        request,
                        cancellationToken),

                RecoveryStage.BuildHumanReviewCorrection =>
                    await CheckBuildReportAsync(
                        request,
                        ArtifactType.BuildHumanReviewCorrectionReport,
                        cancellationToken),

                RecoveryStage.BuildGovernedHumanCorrection =>
                    await CheckBuildReportAsync(
                        request,
                        ArtifactType.BuildHumanReviewCorrectionReport,
                        cancellationToken),

                RecoveryStage.Test =>
                    await CheckTestAsync(
                        request,
                        cancellationToken),

                RecoveryStage.TestHumanReviewCorrection =>
                    await CheckTestArtifactsAsync(
                        request,
                        ArtifactType.TestHumanReviewCorrectionReport,
                        ArtifactType.TestHumanReviewCorrectionResults,
                        cancellationToken),

                RecoveryStage.TestGovernedHumanCorrection =>
                    await CheckTestArtifactsAsync(
                        request,
                        ArtifactType.TestHumanReviewCorrectionReport,
                        ArtifactType.TestHumanReviewCorrectionResults,
                        cancellationToken),

                RecoveryStage.Reviewer =>
                    await CheckReviewerAsync(
                        request,
                        preferHumanReviewCorrection: true,
                        cancellationToken),

                RecoveryStage.ReviewerOriginal =>
                    await CheckReviewerArtifactsAsync(
                        request,
                        ArtifactType.ReviewerReview,
                        ArtifactType.ReviewerResponse,
                        cancellationToken),

                RecoveryStage.ReviewerHumanReviewCorrection =>
                    await CheckReviewerArtifactsAsync(
                        request,
                        ArtifactType.ReviewerHumanReviewCorrectionReview,
                        ArtifactType.ReviewerHumanReviewCorrectionResponse,
                        cancellationToken),

                RecoveryStage.ReviewerHumanReviewCorrectionSuperseding =>
                    await CheckSupersedingReviewerAsync(
                        request,
                        cancellationToken),

                RecoveryStage.ReviewerHumanReviewCorrectionSourceAwareSuperseding =>
                    await CheckSourceAwareSupersedingReviewerAsync(
                        request,
                        cancellationToken),

                RecoveryStage.HumanReviewApproval =>
                    await CheckReportAsync<HumanReviewApprovalEvidence>(
                        request,
                        ArtifactType.HumanReviewApprovalEvidence,
                        value => value.JobId == request.JobId &&
                            value.RunId == request.RunId &&
                            value.AttemptCount == request.AttemptCount &&
                            value.Decision == HumanReviewDecision.Approved &&
                            !string.IsNullOrWhiteSpace(value.Actor) &&
                            value.CorrelationId == request.CorrelationId &&
                            IsSha256(value.ReviewerReviewSha256) &&
                            IsSha256(value.DeterministicAcceptanceGateSha256) &&
                            Enum.IsDefined(value.EffectiveProposalLineage) &&
                            IsSha256(value.EffectiveProposalSha256) &&
                            IsSha256(value.ObservedChangesSha256) &&
                            IsSha256(value.BuildReportSha256) &&
                            IsSha256(value.TestReportSha256) &&
                            IsSha256(value.EffectiveSourceSnapshotSha256),
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
            CancellationToken cancellationToken,
            Func<TReport, StageRecoveryResult>? completed = null)
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

        return completed is null
            ? StageRecoveryResult.Completed()
            : completed(report);
    }

    private async Task<StageRecoveryResult>
        CheckPlanningAsync(
            StageRecoveryRequest request,
            CancellationToken cancellationToken)
    {
        StageRecoveryResult response =
            await CheckArtifactAsync(
                request,
                ArtifactType.AiResponse,
                artifactOptions.MaxArtifactBytes,
                cancellationToken);

        if (!response.IsCompleted)
        {
            return response;
        }

        ArtifactReadResult planRead =
            await ReadAsync(
                request,
                ArtifactType.PlanningPlan,
                MaxReportBytes,
                cancellationToken);

        if (planRead.FailureKind ==
            ArtifactReadFailureKind.NotFound)
        {
            return StageRecoveryResult.NotCompleted();
        }

        if (!planRead.IsSuccess)
        {
            return MapReadFailure(planRead);
        }

        PlannerPlan? plan;

        try
        {
            plan = JsonSerializer.Deserialize<PlannerPlan>(
                planRead.Content.Span);
        }
        catch (JsonException)
        {
            return StageRecoveryResult.InvalidEvidence(
                "STAGE_RECOVERY_REPORT_INVALID_JSON");
        }

        if (plan is null ||
            !IsValidPlan(plan))
        {
            return StageRecoveryResult.InvalidEvidence(
                "STAGE_RECOVERY_REPORT_NOT_SUCCESSFUL");
        }

        PlannerPlanPolicyResult policy =
            plannerPlanPolicy.Validate(
                plan,
                request.JobRequest);

        if (!policy.IsSuccess)
        {
            return StageRecoveryResult.InvalidEvidence(
                "STAGE_RECOVERY_PLANNING_POLICY_INVALID");
        }

        ArtifactReadResult contextRead =
            await ReadAsync(
                request,
                ArtifactType.ContextPackage,
                artifactOptions.MaxArtifactBytes,
                cancellationToken);

        if (!contextRead.IsSuccess)
        {
            return MapReadFailure(contextRead);
        }

        IReadOnlyList<string> priorityPaths =
            priorityPathSelector.Select(
                contextRead.Content,
                request.JobRequest);

        return PlanningPriorityPathSelector
            .HasTopFivePlanPathOverlap(
                plan,
                priorityPaths)
            ? StageRecoveryResult.Completed(
                plannerPlan: plan)
            : StageRecoveryResult.InvalidEvidence(
                "STAGE_RECOVERY_PLANNING_COHERENCE_INVALID");
    }

    private async Task<StageRecoveryResult>
        CheckDeveloperAsync(
            StageRecoveryRequest request,
            bool preferBuildCorrection,
            bool correctionOnly,
            CancellationToken cancellationToken)
    {
        StageRecoveryResult original = await CheckDeveloperArtifactsAsync(
            request,
            ArtifactType.DeveloperProposal,
            ArtifactType.DeveloperResponse,
            cancellationToken);

        if (!preferBuildCorrection)
        {
            return original;
        }

        StageRecoveryResult correction;
        if (request.Stage == RecoveryStage.DeveloperBuildCorrection)
        {
            correction = await CheckDeveloperArtifactsAsync(
                request,
                ArtifactType.DeveloperBuildCorrectionProposal,
                ArtifactType.DeveloperBuildCorrectionResponse,
                cancellationToken);
        }
        else
        {
            correction = await CheckDeveloperArtifactsAsync(
                request,
                ArtifactType.DeveloperBuildCorrectionRetryProposal,
                ArtifactType.DeveloperBuildCorrectionRetryResponse,
                cancellationToken);
            if (request.Stage != RecoveryStage.DeveloperBuildCorrectionRetry &&
                correction.Status == StageRecoveryStatus.NotCompleted)
                correction = await CheckDeveloperArtifactsAsync(
                    request,
                    ArtifactType.DeveloperBuildCorrectionProposal,
                    ArtifactType.DeveloperBuildCorrectionResponse,
                    cancellationToken);
        }

        if (correction.Status == StageRecoveryStatus.NotCompleted)
        {
            return correctionOnly ? correction : original;
        }

        if (!correction.IsCompleted ||
            correction.DeveloperProposal is null)
        {
            return correction;
        }

        if (!original.IsCompleted ||
            original.DeveloperProposal is null)
        {
            return StageRecoveryResult.InvalidEvidence(
                "STAGE_RECOVERY_DEVELOPER_CORRECTION_WITHOUT_ORIGINAL");
        }

        if (!IsValidBuildCorrectionProposal(
                correction.DeveloperProposal,
                original.DeveloperProposal))
        {
            return StageRecoveryResult.InvalidEvidence(
                "STAGE_RECOVERY_DEVELOPER_CORRECTION_INVALID");
        }

        return correctionOnly
            ? correction
            : StageRecoveryResult.Completed(
                MergeBuildCorrectionProposal(
                    original.DeveloperProposal,
                    correction.DeveloperProposal));
    }

    private async Task<StageRecoveryResult>
        CheckHumanReviewCorrectionAsync(
            StageRecoveryRequest request,
            CancellationToken cancellationToken)
    {
        return await CheckReportAsync<HumanReviewCorrectionEvidence>(
            request,
            ArtifactType.HumanReviewCorrectionEvidence,
            evidence => evidence.JobId == request.JobId &&
                evidence.RunId == request.RunId &&
                evidence.Decision == HumanReviewDecision.ChangesRequired &&
                evidence.RequiredCorrections is { Count: > 0 and <= 20 } &&
                evidence.RequiredCorrections.All(correction =>
                    IsSafeRelativePath(correction.RelativePath) &&
                    !string.IsNullOrWhiteSpace(correction.Instruction)),
            cancellationToken,
            evidence => StageRecoveryResult.Completed(
                humanReviewCorrection: evidence));
    }

    private async Task<StageRecoveryResult>
        CheckHumanDeveloperAsync(
            StageRecoveryRequest request,
            bool humanCorrectionOnly,
            CancellationToken cancellationToken)
    {
        StageRecoveryResult current = await CheckDeveloperAsync(
            request,
            preferBuildCorrection: true,
            correctionOnly: false,
            cancellationToken);

        StageRecoveryResult humanEvidence =
            await CheckHumanReviewCorrectionAsync(request, cancellationToken);

        StageRecoveryResult correction = await CheckDeveloperArtifactsAsync(
            request,
            ArtifactType.DeveloperHumanReviewCorrectionProposal,
            ArtifactType.DeveloperHumanReviewCorrectionResponse,
            cancellationToken);

        if (correction.Status == StageRecoveryStatus.NotCompleted)
            return humanCorrectionOnly ? correction : current;

        if (!correction.IsCompleted ||
            correction.DeveloperProposal is null)
            return correction;

        if (!current.IsCompleted || current.DeveloperProposal is null)
            return StageRecoveryResult.InvalidEvidence(
                "STAGE_RECOVERY_HUMAN_CORRECTION_WITHOUT_PROPOSAL");

        if (!humanEvidence.IsCompleted ||
            humanEvidence.HumanReviewCorrection is null)
            return StageRecoveryResult.InvalidEvidence(
                "STAGE_RECOVERY_HUMAN_CORRECTION_EVIDENCE_MISSING");

        if (!IsValidBuildCorrectionProposal(
                correction.DeveloperProposal,
                current.DeveloperProposal) ||
            !HasExactCorrectionPaths(
                correction.DeveloperProposal,
                humanEvidence.HumanReviewCorrection))
            return StageRecoveryResult.InvalidEvidence(
                "STAGE_RECOVERY_HUMAN_CORRECTION_INVALID");

        return humanCorrectionOnly
            ? correction
            : StageRecoveryResult.Completed(
                MergeBuildCorrectionProposal(
                    current.DeveloperProposal,
                    correction.DeveloperProposal));
    }

    private async Task<StageRecoveryResult>
        CheckEffectiveDeveloperProposalAsync(
            StageRecoveryRequest request,
            CancellationToken cancellationToken)
    {
        StageRecoveryResult governed = await CheckGovernedHumanCorrectionAsync(
            request, cancellationToken);
        if (governed.IsCompleted)
            return governed;
        if (governed.Status != StageRecoveryStatus.NotCompleted)
            return governed;

        StageRecoveryResult humanEvidence =
            await CheckHumanReviewCorrectionAsync(request, cancellationToken);
        StageRecoveryResult humanProposal = await CheckHumanDeveloperAsync(
            request,
            humanCorrectionOnly: true,
            cancellationToken);

        if (humanEvidence.IsCompleted)
        {
            if (humanProposal.Status == StageRecoveryStatus.NotCompleted)
            {
                return StageRecoveryResult.InvalidEvidence(
                    "STAGE_RECOVERY_HUMAN_CORRECTION_PROPOSAL_MISSING");
            }

            if (!humanProposal.IsCompleted ||
                humanProposal.DeveloperProposal is null)
            {
                return humanProposal;
            }

            return StageRecoveryResult.Completed(
                developerProposal: humanProposal.DeveloperProposal,
                developerProposalLineage:
                    DeveloperProposalLineage.HumanReviewCorrection);
        }

        if (humanEvidence.Status != StageRecoveryStatus.NotCompleted)
        {
            return humanEvidence;
        }

        if (humanProposal.Status != StageRecoveryStatus.NotCompleted)
        {
            return humanProposal.IsCompleted
                ? StageRecoveryResult.InvalidEvidence(
                    "STAGE_RECOVERY_HUMAN_CORRECTION_EVIDENCE_MISSING")
                : humanProposal;
        }

        StageRecoveryResult buildCorrection = await CheckDeveloperAsync(
            request,
            preferBuildCorrection: true,
            correctionOnly: true,
            cancellationToken);
        if (buildCorrection.IsCompleted &&
            buildCorrection.DeveloperProposal is not null)
        {
            return StageRecoveryResult.Completed(
                developerProposal: buildCorrection.DeveloperProposal,
                developerProposalLineage:
                    DeveloperProposalLineage.BuildCorrection);
        }

        if (buildCorrection.Status != StageRecoveryStatus.NotCompleted)
        {
            return buildCorrection;
        }

        StageRecoveryResult original = await CheckDeveloperAsync(
            request,
            preferBuildCorrection: false,
            correctionOnly: false,
            cancellationToken);
        return original.IsCompleted && original.DeveloperProposal is not null
            ? StageRecoveryResult.Completed(
                developerProposal: original.DeveloperProposal,
                developerProposalLineage: DeveloperProposalLineage.Original)
            : original;
    }

    private async Task<StageRecoveryResult>
        CheckGovernedHumanCorrectionAsync(
            StageRecoveryRequest request,
            CancellationToken cancellationToken)
    {
        StageRecoveryResult evidenceResult =
            await CheckReportAsync<GovernedHumanCorrectionEvidence>(
                request,
                ArtifactType.GovernedHumanCorrectionRequest,
                evidence =>
                    evidence.JobId == request.JobId &&
                    evidence.RunId == request.RunId &&
                    evidence.AttemptCount == request.AttemptCount &&
                    evidence.Stage == "Building" &&
                    !string.IsNullOrWhiteSpace(evidence.Actor) &&
                    !string.IsNullOrWhiteSpace(evidence.CorrelationId) &&
                    IsSha256(evidence.RequestSha256) &&
                    evidence.AllowedPaths.Count > 0 &&
                    evidence.Proposal.Changes.Count > 0 &&
                    evidence.Proposal.Changes.All(change =>
                        change.Operation == DeveloperChangeOperationType.ReplaceFile &&
                        evidence.AllowedPaths.Contains(
                            change.RelativePath,
                            StringComparer.OrdinalIgnoreCase)),
                cancellationToken,
                evidence => StageRecoveryResult.Completed(
                    developerProposal: evidence.Proposal,
                    developerProposalLineage:
                        DeveloperProposalLineage.GovernedHumanCorrection,
                    governedHumanCorrection: evidence));
        if (!evidenceResult.IsCompleted)
            return evidenceResult;

        StageRecoveryResult receiptResult =
            await CheckReportAsync<GovernedHumanCorrectionReceipt>(
                request,
                ArtifactType.GovernedHumanCorrectionReceipt,
                receipt =>
                    receipt.JobId == request.JobId &&
                    receipt.RunId == request.RunId &&
                    receipt.AttemptCount == request.AttemptCount &&
                    receipt.CorrelationId ==
                        evidenceResult.GovernedHumanCorrection!.CorrelationId &&
                    receipt.RequestSha256 ==
                        evidenceResult.GovernedHumanCorrection.RequestSha256 &&
                    receipt.Changes.Count ==
                        evidenceResult.DeveloperProposal!.Changes.Count &&
                    receipt.Changes.All(change =>
                        evidenceResult.DeveloperProposal.Changes.Any(proposed =>
                            string.Equals(
                                proposed.RelativePath,
                                change.RelativePath,
                                StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(
                                proposed.ExpectedContentSha256,
                                change.BeforeSha256,
                                StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(
                                HashContent(proposed.Content),
                                change.AfterSha256,
                                StringComparison.OrdinalIgnoreCase))),
                cancellationToken,
                receipt => StageRecoveryResult.Completed(
                    governedHumanCorrectionReceipt: receipt));
        return receiptResult.IsCompleted
            ? StageRecoveryResult.Completed(
                developerProposal: evidenceResult.DeveloperProposal,
                developerProposalLineage:
                    DeveloperProposalLineage.GovernedHumanCorrection,
                governedHumanCorrection:
                    evidenceResult.GovernedHumanCorrection,
                governedHumanCorrectionReceipt:
                    receiptResult.GovernedHumanCorrectionReceipt)
            : receiptResult;
    }

    private static bool HasExactCorrectionPaths(
        ValidatedDeveloperProposal proposal,
        HumanReviewCorrectionEvidence evidence)
    {
        HashSet<string> required = evidence.RequiredCorrections
            .Select(item => item.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> proposed = proposal.Changes
            .Select(item => item.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return required.SetEquals(proposed);
    }

    private async Task<StageRecoveryResult>
        CheckDeveloperArtifactsAsync(
            StageRecoveryRequest request,
            ArtifactType proposalArtifactType,
            ArtifactType responseArtifactType,
            CancellationToken cancellationToken)
    {
        ArtifactReadResult proposalRead = await ReadAsync(
            request, proposalArtifactType,
            artifactOptions.MaxArtifactBytes, cancellationToken);
        ArtifactReadResult responseRead = await ReadAsync(
            request, responseArtifactType,
            artifactOptions.MaxArtifactBytes, cancellationToken);

        bool proposalMissing = proposalRead.FailureKind ==
            ArtifactReadFailureKind.NotFound;
        bool responseMissing = responseRead.FailureKind ==
            ArtifactReadFailureKind.NotFound;

        if (proposalMissing && responseMissing)
            return StageRecoveryResult.NotCompleted();

        if (proposalMissing || responseMissing)
            return StageRecoveryResult.InvalidEvidence(
                "STAGE_RECOVERY_DEVELOPER_EVIDENCE_PARTIAL");

        if (!proposalRead.IsSuccess) return MapReadFailure(proposalRead);
        if (!responseRead.IsSuccess) return MapReadFailure(responseRead);

        if (proposalRead.Artifact is null ||
            responseRead.Artifact is null ||
            !string.Equals(
                proposalRead.Artifact.CorrelationId,
                responseRead.Artifact.CorrelationId,
                StringComparison.Ordinal))
            return StageRecoveryResult.InvalidEvidence(
                "STAGE_RECOVERY_DEVELOPER_LINEAGE_MISMATCH");

        string proposalJson;
        try
        {
            proposalJson = System.Text.Encoding.UTF8
                .GetString(proposalRead.Content.Span);
        }
        catch
        {
            return StageRecoveryResult.InvalidEvidence(
                "STAGE_RECOVERY_DEVELOPER_PROPOSAL_ENCODING_INVALID");
        }

        if (!structuredValidator.TryValidate(
                DeveloperContractSchema.CreateSchema(),
                proposalJson, out _))
            return StageRecoveryResult.InvalidEvidence(
                "STAGE_RECOVERY_DEVELOPER_PROPOSAL_SCHEMA_INVALID");

        DeveloperProposal? proposal;
        AiResponse? response;
        try
        {
            proposal = JsonSerializer.Deserialize<DeveloperProposal>(
                proposalRead.Content.Span);
            response = JsonSerializer.Deserialize<AiResponse>(
                responseRead.Content.Span);
        }
        catch (JsonException)
        {
            return StageRecoveryResult.InvalidEvidence(
                "STAGE_RECOVERY_DEVELOPER_JSON_INVALID");
        }

        DeveloperProposalPolicyResult policy =
            developerPolicy.Validate(proposal);
        if (!policy.IsSuccess || policy.Proposal is null)
            return StageRecoveryResult.InvalidEvidence(
                "STAGE_RECOVERY_DEVELOPER_POLICY_INVALID");

        if (response is null || !response.IsSuccess)
            return StageRecoveryResult.InvalidEvidence(
                "STAGE_RECOVERY_DEVELOPER_RESPONSE_INVALID");

        return StageRecoveryResult.Completed(policy.Proposal);
    }

    private async Task<StageRecoveryResult>
        CheckObservedChangesAsync(
            StageRecoveryRequest request,
            CancellationToken cancellationToken)
    {
        StageRecoveryResult retry =
            await CheckObservedChangesArtifactAsync(
                request,
                ArtifactType.ObservedBuildCorrectionRetryManifest,
                cancellationToken);
        if (retry.Status != StageRecoveryStatus.NotCompleted)
            return retry;

        StageRecoveryResult correction =
            await CheckReportAsync<ObservedChangeManifest>(
                request,
                ArtifactType.ObservedBuildCorrectionManifest,
                manifest => manifest.JobId == request.JobId &&
                    manifest.RunId == request.RunId &&
                    manifest.Entries is not null,
                cancellationToken,
                manifest => StageRecoveryResult.Completed(
                    observedChangeManifest: manifest));

        return correction.Status == StageRecoveryStatus.NotCompleted
            ? await CheckReportAsync<ObservedChangeManifest>(
                request,
                ArtifactType.ObservedChangeManifest,
                manifest => manifest.JobId == request.JobId &&
                    manifest.RunId == request.RunId &&
                    manifest.Entries is not null,
                cancellationToken,
                manifest => StageRecoveryResult.Completed(
                    observedChangeManifest: manifest))
            : correction;
    }

    private Task<StageRecoveryResult> CheckObservedChangesArtifactAsync(
        StageRecoveryRequest request,
        ArtifactType artifactType,
        CancellationToken cancellationToken) =>
        CheckReportAsync<ObservedChangeManifest>(
            request,
            artifactType,
            manifest => IsValidObservedManifest(
                request,
                artifactType,
                manifest),
            cancellationToken,
            manifest => StageRecoveryResult.Completed(
                observedChangeManifest: manifest));

    private static bool IsValidObservedManifest(
        StageRecoveryRequest request,
        ArtifactType artifactType,
        ObservedChangeManifest manifest)
    {
        if (manifest.JobId != request.JobId ||
            manifest.RunId != request.RunId ||
            manifest.Entries is null)
            return false;

        if (artifactType !=
            ArtifactType.ObservedGovernedHumanCorrectionManifest)
            return true;

        return manifest.AttemptCount == request.AttemptCount &&
            !string.IsNullOrWhiteSpace(manifest.ProposalLineageId) &&
            IsSha256(manifest.ProposalFingerprintSha256) &&
            !string.IsNullOrWhiteSpace(
                manifest.SafeChangeReceiptReference) &&
            manifest.Entries.Count > 0 &&
            manifest.Entries.All(entry =>
                entry.ProposalOperation is not null &&
                entry.GitChangeKind is not null &&
                entry.ValidationResult == "PASS");
    }

    private async Task<StageRecoveryResult> CheckBuildAsync(
        StageRecoveryRequest request,
        CancellationToken cancellationToken)
    {
        ArtifactReadResult retryRead = await ReadAsync(
            request,
            ArtifactType.BuildCorrectionRetryReport,
            MaxReportBytes,
            cancellationToken);
        if (retryRead.FailureKind != ArtifactReadFailureKind.NotFound)
            return ParseBuildReport(
                request,
                retryRead,
                allowFailedExecution: false,
                standardOutput: null);

        ArtifactReadResult correctionRead = await ReadAsync(
            request,
            ArtifactType.BuildCorrectionReport,
            MaxReportBytes,
            cancellationToken);

        if (correctionRead.FailureKind != ArtifactReadFailureKind.NotFound)
        {
            StageRecoveryResult correctedBuild = ParseBuildReport(
                request,
                correctionRead,
                allowFailedExecution: false,
                standardOutput: null);
            if (correctedBuild.IsCompleted ||
                correctedBuild.ErrorCode != "STAGE_RECOVERY_REPORT_NOT_SUCCESSFUL")
                return correctedBuild;

            StageRecoveryResult previousCorrection =
                await CheckDeveloperArtifactsAsync(
                    request,
                    ArtifactType.DeveloperBuildCorrectionProposal,
                    ArtifactType.DeveloperBuildCorrectionResponse,
                    cancellationToken);
            if (!previousCorrection.IsCompleted ||
                previousCorrection.DeveloperProposal is null ||
                !BuildCorrectionNoOpPolicy.IsEntireNoOp(
                    previousCorrection.DeveloperProposal))
                return correctedBuild;
            // A failed no-op build did not alter source. Keep the original
            // compiler evidence available for one governed correction retry.
        }

        ArtifactReadResult reportRead = await ReadAsync(
            request,
            ArtifactType.BuildReport,
            MaxReportBytes,
            cancellationToken);

        if (reportRead.FailureKind == ArtifactReadFailureKind.NotFound)
        {
            return StageRecoveryResult.NotCompleted();
        }

        StageRecoveryResult originalResult = ParseBuildReport(
            request,
            reportRead,
            allowFailedExecution: false,
            standardOutput: null);

        if (originalResult.IsCompleted ||
            !string.Equals(
                originalResult.ErrorCode,
                "STAGE_RECOVERY_REPORT_NOT_SUCCESSFUL",
                StringComparison.Ordinal))
        {
            return originalResult;
        }

        ArtifactReadResult outputRead = await ReadAsync(
            request,
            ArtifactType.BuildStandardOutput,
            artifactOptions.MaxArtifactBytes,
            cancellationToken);

        string? output = outputRead.IsSuccess
            ? Encoding.UTF8.GetString(outputRead.Content.Span)
            : null;

        return ParseBuildReport(
            request,
            reportRead,
            allowFailedExecution: true,
            output);
    }

    private async Task<StageRecoveryResult> CheckBuildReportAsync(
        StageRecoveryRequest request,
        ArtifactType artifactType,
        CancellationToken cancellationToken)
    {
        ArtifactReadResult read = await ReadAsync(
            request,
            artifactType,
            MaxReportBytes,
            cancellationToken);

        if (read.FailureKind == ArtifactReadFailureKind.NotFound)
            return StageRecoveryResult.NotCompleted();

        return ParseBuildReport(
            request,
            read,
            allowFailedExecution: false,
            standardOutput: null);
    }

    private static StageRecoveryResult ParseBuildReport(
        StageRecoveryRequest request,
        ArtifactReadResult read,
        bool allowFailedExecution,
        string? standardOutput)
    {
        if (!read.IsSuccess)
        {
            return MapReadFailure(read);
        }

        BuildExecutionReport? report;

        try
        {
            report = JsonSerializer.Deserialize<BuildExecutionReport>(
                read.Content.Span);
        }
        catch (JsonException)
        {
            return StageRecoveryResult.InvalidEvidence(
                "STAGE_RECOVERY_REPORT_INVALID_JSON");
        }

        if (report is null ||
            report.JobId != request.JobId ||
            report.RunId != request.RunId)
        {
            return StageRecoveryResult.InvalidEvidence(
                "STAGE_RECOVERY_REPORT_NOT_SUCCESSFUL");
        }

        if (report.IsSuccess)
        {
            return StageRecoveryResult.Completed(buildReport: report);
        }

        return allowFailedExecution &&
            !string.IsNullOrWhiteSpace(standardOutput)
            ? StageRecoveryResult.FailedBuild(report, standardOutput)
            : StageRecoveryResult.InvalidEvidence(
                "STAGE_RECOVERY_REPORT_NOT_SUCCESSFUL");
    }

    private static bool IsValidBuildCorrectionProposal(
        ValidatedDeveloperProposal correction,
        ValidatedDeveloperProposal original)
    {
        var hashes = original.Changes.ToDictionary(
            change => change.RelativePath,
            change => Convert.ToHexString(
                    SHA256.HashData(
                        Encoding.UTF8.GetBytes(change.Content)))
                .ToLowerInvariant(),
            StringComparer.OrdinalIgnoreCase);

        return correction.Changes.Count > 0 &&
            correction.Changes.All(change =>
                change.Operation ==
                    DeveloperChangeOperationType.ReplaceFile &&
                hashes.TryGetValue(change.RelativePath, out string? hash) &&
                string.Equals(
                    change.ExpectedContentSha256,
                    hash,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static ValidatedDeveloperProposal MergeBuildCorrectionProposal(
        ValidatedDeveloperProposal original,
        ValidatedDeveloperProposal correction)
    {
        var corrections = correction.Changes.ToDictionary(
            change => change.RelativePath,
            StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<ValidatedDeveloperChange> changes =
            original.Changes.Select(change =>
            {
                if (!corrections.TryGetValue(
                        change.RelativePath,
                        out ValidatedDeveloperChange? replacement))
                {
                    return change;
                }

                return change with
                {
                    Content = replacement.Content,
                    Intent = replacement.Intent,
                    ContentBytes = Encoding.UTF8.GetByteCount(
                        replacement.Content)
                };
            }).ToArray();

        long totalChangeBytes = changes.Sum(change => change.ContentBytes);

        return new ValidatedDeveloperProposal(
            original.Summary,
            changes,
            original.Assumptions,
            original.Risks,
            totalChangeBytes,
            original.ProposalBytes +
                totalChangeBytes - original.TotalChangeBytes);
    }

    private async Task<StageRecoveryResult>
        CheckTestAsync(
            StageRecoveryRequest request,
            CancellationToken cancellationToken)
    {
        return await CheckTestArtifactsAsync(
            request,
            ArtifactType.TestReport,
            ArtifactType.TestResults,
            cancellationToken);
    }

    private async Task<StageRecoveryResult> CheckTestArtifactsAsync(
        StageRecoveryRequest request,
        ArtifactType reportArtifactType,
        ArtifactType resultsArtifactType,
        CancellationToken cancellationToken)
    {
        StageRecoveryResult report =
            await CheckReportAsync<TestExecutionReport>(
                request,
                reportArtifactType,
                value => value.IsSuccess &&
                    value.JobId == request.JobId &&
                    value.RunId == request.RunId,
                cancellationToken,
                value => StageRecoveryResult.Completed(
                    testReport: value));

        if (!report.IsCompleted)
        {
            return report;
        }

        StageRecoveryResult results = await CheckArtifactAsync(
            request,
            resultsArtifactType,
            artifactOptions.MaxArtifactBytes,
            cancellationToken);

        return results.IsCompleted
            ? StageRecoveryResult.Completed(testReport: report.TestReport)
            : results;
    }

    private async Task<StageRecoveryResult> CheckReviewerAsync(
        StageRecoveryRequest request,
        bool preferHumanReviewCorrection,
        CancellationToken cancellationToken)
    {
        if (preferHumanReviewCorrection)
        {
            StageRecoveryResult sourceAware =
                await CheckSourceAwareSupersedingReviewerAsync(
                    request,
                    cancellationToken);
            if (sourceAware.Status != StageRecoveryStatus.NotCompleted)
                return sourceAware;

            StageRecoveryResult superseding =
                await CheckSupersedingReviewerAsync(
                    request,
                    cancellationToken);
            if (superseding.Status != StageRecoveryStatus.NotCompleted)
                return superseding;

            StageRecoveryResult human =
                await CheckReviewerArtifactsAsync(
                    request,
                    ArtifactType.ReviewerHumanReviewCorrectionReview,
                    ArtifactType.ReviewerHumanReviewCorrectionResponse,
                    cancellationToken);
            if (human.Status != StageRecoveryStatus.NotCompleted)
                return human;
        }

        return await CheckReviewerArtifactsAsync(
            request,
            ArtifactType.ReviewerReview,
            ArtifactType.ReviewerResponse,
            cancellationToken);
    }

    private async Task<StageRecoveryResult> CheckSupersedingReviewerAsync(
        StageRecoveryRequest request,
        CancellationToken cancellationToken)
    {
        StageRecoveryResult review = await CheckReviewerArtifactsAsync(
            request,
            ArtifactType.ReviewerHumanReviewCorrectionSupersedingReview,
            ArtifactType.ReviewerHumanReviewCorrectionSupersedingResponse,
            cancellationToken);
        if (!review.IsCompleted)
            return review;

        StageRecoveryResult evidence = await CheckReportAsync<ReviewerSupersessionEvidence>(
            request,
            ArtifactType.ReviewerHumanReviewCorrectionSupersessionEvidence,
            value => value.JobId == request.JobId &&
                value.RunId == request.RunId &&
                value.SupersededReviewArtifactType ==
                    ArtifactType.ReviewerHumanReviewCorrectionReview &&
                value.EffectiveProposalLineage ==
                    DeveloperProposalLineage.HumanReviewCorrection &&
                value.Decision == review.ReviewerReview!.Decision &&
                IsSha256(value.EffectiveProposalSha256) &&
                IsSha256(value.ObservedChangesSha256) &&
                IsSha256(value.BuildReportSha256) &&
                IsSha256(value.TestReportSha256),
            cancellationToken);
        return evidence.IsCompleted
            ? review
            : evidence.Status == StageRecoveryStatus.NotCompleted
                ? StageRecoveryResult.InvalidEvidence(
                    "STAGE_RECOVERY_REVIEWER_SUPERSESSION_EVIDENCE_PARTIAL")
                : evidence;
    }

    private async Task<StageRecoveryResult> CheckSourceAwareSupersedingReviewerAsync(
        StageRecoveryRequest request,
        CancellationToken cancellationToken)
    {
        StageRecoveryResult review = await CheckReviewerArtifactsAsync(
            request,
            ArtifactType.ReviewerHumanReviewCorrectionDeterministicSupersedingReview,
            ArtifactType.ReviewerHumanReviewCorrectionDeterministicSupersedingResponse,
            cancellationToken);
        if (!review.IsCompleted)
            return review;

        StageRecoveryResult evidence = await CheckReportAsync<ReviewerSupersessionEvidence>(
            request,
            ArtifactType.ReviewerHumanReviewCorrectionDeterministicSupersessionEvidence,
            value => value.JobId == request.JobId &&
                value.RunId == request.RunId &&
                value.SupersededReviewArtifactType ==
                    ArtifactType.ReviewerHumanReviewCorrectionReview &&
                value.EffectiveProposalLineage ==
                    DeveloperProposalLineage.HumanReviewCorrection &&
                value.ReviewVersion == 3 &&
                value.Decision == review.ReviewerReview!.Decision &&
                IsSha256(value.EffectiveProposalSha256) &&
                IsSha256(value.EffectiveSourceSnapshotSha256) &&
                IsSha256(value.DeterministicAcceptanceGateSha256) &&
                IsSha256(value.ObservedChangesSha256) &&
                IsSha256(value.BuildReportSha256) &&
                IsSha256(value.TestReportSha256),
            cancellationToken);
        return evidence.IsCompleted
            ? review
            : evidence.Status == StageRecoveryStatus.NotCompleted
                ? StageRecoveryResult.InvalidEvidence(
                    "STAGE_RECOVERY_SOURCE_AWARE_REVIEWER_SUPERSESSION_EVIDENCE_PARTIAL")
                : evidence;
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string HashContent(string content) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(content)))
        .ToLowerInvariant();

    private async Task<StageRecoveryResult> CheckReviewerArtifactsAsync(
        StageRecoveryRequest request,
        ArtifactType reviewArtifactType,
        ArtifactType responseArtifactType,
        CancellationToken cancellationToken)
    {
        ArtifactReadResult reviewRead = await ReadAsync(request, reviewArtifactType, MaxReportBytes, cancellationToken);
        ArtifactReadResult responseRead = await ReadAsync(request, responseArtifactType, MaxReportBytes, cancellationToken);
        bool reviewMissing = reviewRead.FailureKind == ArtifactReadFailureKind.NotFound;
        bool responseMissing = responseRead.FailureKind == ArtifactReadFailureKind.NotFound;
        if (reviewMissing && responseMissing) return StageRecoveryResult.NotCompleted();
        if (reviewMissing || responseMissing) return StageRecoveryResult.InvalidEvidence("STAGE_RECOVERY_REVIEWER_EVIDENCE_PARTIAL");
        if (!reviewRead.IsSuccess) return MapReadFailure(reviewRead);
        if (!responseRead.IsSuccess) return MapReadFailure(responseRead);
        ReviewerReview? review;
        AiResponse? response;
        try
        {
            review = JsonSerializer.Deserialize<ReviewerReview>(reviewRead.Content.Span);
            response = JsonSerializer.Deserialize<AiResponse>(responseRead.Content.Span);
        }
        catch (JsonException)
        { return StageRecoveryResult.InvalidEvidence("STAGE_RECOVERY_REVIEWER_JSON_INVALID"); }
        string modelContractJson = JsonSerializer.Serialize(new
        {
            decision = review?.Decision,
            findings = review?.Findings,
            requiredCorrections = review?.RequiredCorrections,
            riskAssessment = review?.RiskAssessment,
            summary = review?.Summary
        });
        if (!structuredValidator.TryValidate(
                ReviewerContractSchema.CreateSchema(), modelContractJson, out _))
            return StageRecoveryResult.InvalidEvidence(
                "STAGE_RECOVERY_REVIEWER_SCHEMA_INVALID");
        if (!ReviewerExecutionService.ValidReview(review) || response is null || !response.IsSuccess)
            return StageRecoveryResult.InvalidEvidence("STAGE_RECOVERY_REVIEWER_INVALID");
        return StageRecoveryResult.Completed(reviewerReview: review);
    }

    private static bool IsSafeRelativePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            relativePath.Length > 512)
            return false;

        string normalized = relativePath.Replace('\\', '/');
        if (normalized.StartsWith('/') ||
            normalized.Contains('\0') ||
            normalized.Contains(':'))
            return false;

        return normalized.Split('/').All(segment =>
            segment.Length > 0 && segment is not "." and not "..");
    }

    private static bool IsValidPlan(
        PlannerPlan plan) =>
        plan.Objective is not null &&
        plan.FilesToInspect is not null &&
        plan.CandidateFilesToModify is not null &&
        plan.Strategy is not null &&
        plan.AcceptanceCriteria is not null &&
        plan.Risks is not null &&
        plan.ExpectedTests is not null &&
        plan.Assumptions is not null &&
        plan.Uncertainties is not null;

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
