using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Execution;
using Kronxy.Application.Context;
using Kronxy.Application.Repositories;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Jobs;

namespace Kronxy.Application.Jobs;

public sealed class JobOrchestrator : IJobOrchestrator
{
        private readonly IJobRepository _jobRepository;

        private readonly IUnitOfWork _unitOfWork;

        private readonly IDateTimeProvider _clock;

        private readonly IJobStateMachine _stateMachine;

        private readonly ISourceRevisionProvider _sourceRevisionProvider;

        private readonly IExecutionPlaneLifecycle _executionPlaneLifecycle;

        private readonly IJobRunIdProvider _jobRunIdProvider;

        private readonly IContextGenerationService _contextGenerationService;

        private readonly IExecutionTargetProvider _executionTargetProvider;

        private readonly IRestoreExecutionService _restoreExecutionService;

        private readonly IBuildExecutionService _buildExecutionService;

        private readonly ITestExecutionService _testExecutionService;

        private readonly IPlanningExecutionService _planningExecutionService;

        private readonly IDeveloperExecutionService _developerExecutionService;

        private readonly ISafeChangeApplier _safeChangeApplier;

        private readonly IObservedChangeEvidenceService _observedChangeEvidenceService;

        private readonly IStageRecoveryEvidenceService _stageRecoveryEvidenceService;

        private readonly IReviewerEffectiveSourceSnapshotService _reviewerEffectiveSourceSnapshotService;

        private readonly IReviewerExecutionService _reviewerExecutionService;

        private readonly IReviewDecisionPolicy _reviewDecisionPolicy;

        private readonly IArtifactStore? _artifactStore;
        private readonly IArtifactMetadataRepository? _artifactMetadataRepository;
        private readonly IDeveloperProposalPolicy? _developerProposalPolicy;
        private readonly IDevelopmentAnalysisService? _developmentAnalysisService;

        public JobOrchestrator(
                IJobRepository jobRepository,
                IUnitOfWork unitOfWork,
                IDateTimeProvider clock,
                IJobStateMachine stateMachine,
                ISourceRevisionProvider sourceRevisionProvider,
                IExecutionPlaneLifecycle executionPlaneLifecycle,
                IJobRunIdProvider jobRunIdProvider,
                IContextGenerationService contextGenerationService,
                IExecutionTargetProvider executionTargetProvider,
                IRestoreExecutionService restoreExecutionService,
                IBuildExecutionService buildExecutionService,
                ITestExecutionService testExecutionService,
                IPlanningExecutionService planningExecutionService,
                IDeveloperExecutionService developerExecutionService,
                ISafeChangeApplier safeChangeApplier,
                IObservedChangeEvidenceService observedChangeEvidenceService,
                IStageRecoveryEvidenceService stageRecoveryEvidenceService,
                IReviewerEffectiveSourceSnapshotService reviewerEffectiveSourceSnapshotService,
                IReviewerExecutionService reviewerExecutionService,
                IReviewDecisionPolicy reviewDecisionPolicy,
                IArtifactStore? artifactStore = null,
                IArtifactMetadataRepository? artifactMetadataRepository = null,
                IDeveloperProposalPolicy? developerProposalPolicy = null,
                IDevelopmentAnalysisService? developmentAnalysisService = null)
        {
                _jobRepository =
                        jobRepository ??
                        throw new ArgumentNullException(
                                nameof(jobRepository));

                _unitOfWork =
                        unitOfWork ??
                        throw new ArgumentNullException(
                                nameof(unitOfWork));

                _clock =
                        clock ??
                        throw new ArgumentNullException(
                                nameof(clock));

                _stateMachine =
                        stateMachine ??
                        throw new ArgumentNullException(
                                nameof(stateMachine));

                _sourceRevisionProvider =
                        sourceRevisionProvider ??
                        throw new ArgumentNullException(
                                nameof(sourceRevisionProvider));

                _executionPlaneLifecycle =
                        executionPlaneLifecycle ??
                        throw new ArgumentNullException(
                                nameof(executionPlaneLifecycle));

                _jobRunIdProvider =
                        jobRunIdProvider ??
                        throw new ArgumentNullException(
                                nameof(jobRunIdProvider));

                _contextGenerationService =
                        contextGenerationService ??
                        throw new ArgumentNullException(
                                nameof(contextGenerationService));

                _executionTargetProvider =
                        executionTargetProvider ??
                        throw new ArgumentNullException(
                                nameof(executionTargetProvider));

                _restoreExecutionService =
                        restoreExecutionService ??
                        throw new ArgumentNullException(
                                nameof(restoreExecutionService));

                _buildExecutionService =
                        buildExecutionService ??
                        throw new ArgumentNullException(
                                nameof(buildExecutionService));

                _testExecutionService =
                        testExecutionService ??
                        throw new ArgumentNullException(
                                nameof(testExecutionService));

                _planningExecutionService =
                        planningExecutionService ??
                        throw new ArgumentNullException(
                                nameof(planningExecutionService));

                _developerExecutionService =
                        developerExecutionService ??
                        throw new ArgumentNullException(
                                nameof(developerExecutionService));

                _safeChangeApplier =
                        safeChangeApplier ??
                        throw new ArgumentNullException(
                                nameof(safeChangeApplier));

                _observedChangeEvidenceService = observedChangeEvidenceService ??
                        throw new ArgumentNullException(nameof(observedChangeEvidenceService));

                _stageRecoveryEvidenceService =
                        stageRecoveryEvidenceService ??
                        throw new ArgumentNullException(
                                nameof(stageRecoveryEvidenceService));

                _reviewerEffectiveSourceSnapshotService =
                        reviewerEffectiveSourceSnapshotService ??
                        throw new ArgumentNullException(
                                nameof(reviewerEffectiveSourceSnapshotService));

                _reviewerExecutionService = reviewerExecutionService ??
                        throw new ArgumentNullException(nameof(reviewerExecutionService));
                _reviewDecisionPolicy = reviewDecisionPolicy ??
                        throw new ArgumentNullException(nameof(reviewDecisionPolicy));
                _artifactStore = artifactStore;
                _artifactMetadataRepository = artifactMetadataRepository;
                _developerProposalPolicy = developerProposalPolicy;
                _developmentAnalysisService = developmentAnalysisService;
        }

        public async Task<JobOperationResult> RequestHumanReviewCorrectionAsync(
                Guid jobId,
                HumanReviewCorrectionRequest request,
                CancellationToken cancellationToken = default)
        {
                Job? job = await _jobRepository.GetByIdAsync(jobId, cancellationToken);
                if (job is null)
                        return JobOperationResult.Failure(JobOperationKind.PermanentFailure, JobApplicationErrors.NotFound);
                if (job.State != JobState.WaitingHuman)
                        return JobOperationResult.Failure(JobOperationKind.InvalidTransition, JobApplicationErrors.StageRecoveryFailed);
                if (_artifactStore is null || !ValidHumanReviewRequest(request))
                        return JobOperationResult.Failure(JobOperationKind.PermanentFailure, JobApplicationErrors.StageRecoveryFailed);

                Guid runId = _jobRunIdProvider.Create(job.Id, job.AttemptCount);
                StageRecoveryResult existing = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.HumanReviewCorrection,
                        request.CorrelationId, cancellationToken);
                if (existing.Status != StageRecoveryStatus.NotCompleted)
                        return JobOperationResult.Failure(JobOperationKind.InvalidTransition, JobApplicationErrors.StageRecoveryFailed);

                StageRecoveryResult reviewer = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.ReviewerOriginal,
                        request.CorrelationId, cancellationToken);
                StageRecoveryResult plan = await CheckRecoveryEvidenceAsync(
                        job, _jobRunIdProvider.Create(job.Id, 1), RecoveryStage.Planning,
                        request.CorrelationId, cancellationToken);
                StageRecoveryResult developer = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.Developer,
                        request.CorrelationId, cancellationToken);
                JobOperationResult? evidenceFailure = RecoveryFailure(reviewer) ??
                        RecoveryFailure(plan) ?? RecoveryFailure(developer);
                if (evidenceFailure is not null) return evidenceFailure;
                if (!reviewer.IsCompleted || reviewer.ReviewerReview?.Decision != ReviewerDecision.Approved ||
                    !plan.IsCompleted || plan.PlannerPlan is null ||
                    !developer.IsCompleted || developer.DeveloperProposal is null)
                        return JobOperationResult.Failure(JobOperationKind.PermanentFailure, JobApplicationErrors.ReviewerEvidenceInvalid);

                HashSet<string> allowlist = plan.PlannerPlan.CandidateFilesToModify
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (request.RequiredCorrections.Any(correction => !allowlist.Contains(correction.RelativePath)))
                        return JobOperationResult.Failure(JobOperationKind.PermanentFailure, JobApplicationErrors.StageRecoveryFailed);
                HashSet<string> proposedFiles = developer.DeveloperProposal.Changes
                        .Select(change => change.RelativePath)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (request.RequiredCorrections.Any(correction => !proposedFiles.Contains(correction.RelativePath)))
                        return JobOperationResult.Failure(JobOperationKind.PermanentFailure, JobApplicationErrors.StageRecoveryFailed);

                var evidence = new HumanReviewCorrectionEvidence
                {
                        JobId = job.Id,
                        RunId = runId,
                        Decision = HumanReviewDecision.ChangesRequired,
                        RequiredCorrections = request.RequiredCorrections,
                        RecordedAtUtc = _clock.UtcNow
                };
                ArtifactWriteResult write = await _artifactStore.WriteAsync(
                        new ArtifactWriteRequest
                        {
                                JobId = job.Id,
                                RunId = runId,
                                ArtifactType = ArtifactType.HumanReviewCorrectionEvidence,
                                Content = JsonSerializer.SerializeToUtf8Bytes(evidence),
                                CorrelationId = request.CorrelationId
                        }, cancellationToken);
                if (!write.IsSuccess)
                        return JobOperationResult.Failure(JobOperationKind.PermanentFailure, JobApplicationErrors.StageRecoveryFailed);

                Result transition = job.BeginHumanReviewCorrection(
                        _clock.UtcNow,
                        "Human review requested governed corrections.",
                        request.Actor,
                        request.CorrelationId);
                if (transition.IsFailure)
                        return JobOperationResult.Failure(JobOperationKind.InvalidTransition, transition.Error);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return JobOperationResult.Success();
        }

        public async Task<JobOperationResult> ApproveHumanReviewAsync(
                Guid jobId,
                string actor,
                string correlationId,
                CancellationToken cancellationToken = default)
        {
                if (jobId == Guid.Empty || string.IsNullOrWhiteSpace(actor) ||
                    string.IsNullOrWhiteSpace(correlationId) ||
                    actor.Length > 200 || correlationId.Length > 200 ||
                    actor.IndexOfAny(['\0', '\r', '\n']) >= 0 ||
                    correlationId.IndexOfAny(['\0', '\r', '\n']) >= 0)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.StageRecoveryFailed);

                Job? job = await _jobRepository.GetByIdAsync(jobId, cancellationToken);
                if (job is null)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.NotFound);
                if (_artifactStore is null)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.StageRecoveryFailed);

                Guid runId = _jobRunIdProvider.Create(job.Id, job.AttemptCount);
                StageRecoveryResult existing = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.HumanReviewApproval,
                        correlationId, cancellationToken);
                JobOperationResult? existingFailure = RecoveryFailure(existing);
                if (existingFailure is not null) return existingFailure;

                if (job.State == JobState.Completed)
                        return existing.IsCompleted
                                ? JobOperationResult.Success()
                                : JobOperationResult.Failure(
                                        JobOperationKind.InvalidTransition,
                                        JobApplicationErrors.StageRecoveryFailed);
                if (job.State != JobState.WaitingHuman)
                        return JobOperationResult.Failure(
                                JobOperationKind.InvalidTransition,
                                JobApplicationErrors.StageRecoveryFailed);

                if (!existing.IsCompleted)
                {
                        EffectiveGovernedCorrectionResolution governedResolution =
                                await ResolveEffectiveGovernedCorrectionAsync(
                                        job,
                                        runId,
                                        cancellationToken);
                        if (!governedResolution.IsValid)
                                return JobOperationResult.Failure(
                                        JobOperationKind.PermanentFailure,
                                        JobApplicationErrors.StageRecoveryFailed);
                        bool isGovernedHumanCorrection =
                                governedResolution.CorrelationId is not null;
                        string evidenceCorrelationId =
                                governedResolution.CorrelationId ?? correlationId;
                        Guid planningRunId = _jobRunIdProvider.Create(job.Id, 1);
                        StageRecoveryResult plan = await CheckRecoveryEvidenceAsync(
                                job, planningRunId, RecoveryStage.Planning,
                                correlationId, cancellationToken);
                        StageRecoveryResult developer = await CheckRecoveryEvidenceAsync(
                                job, runId, RecoveryStage.EffectiveDeveloperProposal,
                                evidenceCorrelationId, cancellationToken);
                        JobOperationResult? selectionFailure = RecoveryFailure(plan) ??
                                RecoveryFailure(developer);
                        if (selectionFailure is not null) return selectionFailure;

                        EffectiveEvidenceSelection? selection =
                                ResolveEffectiveEvidenceSelection(
                                        developer.DeveloperProposalLineage);
                        if (!plan.IsCompleted || plan.PlannerPlan is null ||
                            !developer.IsCompleted || developer.DeveloperProposal is null ||
                            selection is null)
                                return JobOperationResult.Failure(
                                        JobOperationKind.PermanentFailure,
                                        JobApplicationErrors.ReviewerEvidenceInvalid);

                        StageRecoveryResult reviewer = await CheckRecoveryEvidenceAsync(
                                job, runId, selection.ApprovalReviewerStage,
                                correlationId, cancellationToken);
                        StageRecoveryResult observed = await CheckRecoveryEvidenceAsync(
                                job, runId, selection.ObservedStage,
                                evidenceCorrelationId, cancellationToken);
                        StageRecoveryResult build = await CheckRecoveryEvidenceAsync(
                                job, runId, selection.BuildStage,
                                evidenceCorrelationId, cancellationToken);
                        StageRecoveryResult test = isGovernedHumanCorrection
                                ? await RecoverEffectiveGovernedTestAsync(
                                        job,
                                        runId,
                                        evidenceCorrelationId,
                                        cancellationToken)
                                : await CheckRecoveryEvidenceAsync(
                                        job, runId, selection.TestStage,
                                        evidenceCorrelationId, cancellationToken);
                        JobOperationResult? evidenceFailure = RecoveryFailure(reviewer) ??
                                RecoveryFailure(observed) ?? RecoveryFailure(build) ??
                                RecoveryFailure(test);
                        if (evidenceFailure is not null) return evidenceFailure;

                        ReviewerReview? review = reviewer.ReviewerReview;
                        if (!reviewer.IsCompleted || review is null ||
                            review.Decision != ReviewerDecision.Approved ||
                            review.RequiredCorrections.Count != 0 ||
                            review.DeterministicAcceptanceGate is null ||
                            !review.DeterministicAcceptanceGate.IsSuccess ||
                            !observed.IsCompleted || observed.ObservedChangeManifest is null ||
                            !build.IsCompleted || build.BuildReport is null ||
                            !build.BuildReport.IsSuccess ||
                            !test.IsCompleted || test.TestReport is null ||
                            !test.TestReport.IsSuccess)
                                return JobOperationResult.Failure(
                                        JobOperationKind.PermanentFailure,
                                        JobApplicationErrors.ReviewerEvidenceInvalid);

                        ReviewerEffectiveSourceSnapshot? effectiveSource =
                                await CaptureReviewerEffectiveSourceAsync(
                                        job,
                                        runId,
                                        plan.PlannerPlan,
                                        developer.DeveloperProposal,
                                        observed.ObservedChangeManifest,
                                        selection.Lineage,
                                        cancellationToken);
                        if (effectiveSource is null ||
                            !ValidApprovalLineage(
                                    job,
                                    developer,
                                    observed.ObservedChangeManifest,
                                    effectiveSource,
                                    selection) ||
                            !ReviewerMatchesEffectiveSource(review, effectiveSource))
                                return JobOperationResult.Failure(
                                        JobOperationKind.PermanentFailure,
                                        JobApplicationErrors.ReviewerEvidenceInvalid);

                        var evidence = new HumanReviewApprovalEvidence
                        {
                                JobId = job.Id,
                                RunId = runId,
                                AttemptCount = job.AttemptCount,
                                Decision = HumanReviewDecision.Approved,
                                Actor = actor,
                                CorrelationId = correlationId,
                                ReviewerReviewSha256 = Hash(review),
                                DeterministicAcceptanceGateSha256 =
                                        Hash(review.DeterministicAcceptanceGate),
                                EffectiveProposalLineage = selection.Lineage,
                                EffectiveProposalSha256 =
                                        Hash(developer.DeveloperProposal),
                                ObservedChangesSha256 =
                                        Hash(observed.ObservedChangeManifest),
                                BuildReportSha256 = Hash(build.BuildReport),
                                TestReportSha256 = Hash(test.TestReport),
                                EffectiveSourceSnapshotSha256 =
                                        Hash(effectiveSource),
                                RecordedAtUtc = _clock.UtcNow
                        };
                        ArtifactWriteResult write = await _artifactStore.WriteAsync(
                                new ArtifactWriteRequest
                                {
                                        JobId = job.Id,
                                        RunId = runId,
                                        ArtifactType = ArtifactType.HumanReviewApprovalEvidence,
                                        Content = JsonSerializer.SerializeToUtf8Bytes(evidence),
                                        CorrelationId = correlationId
                                }, cancellationToken);
                        if (!write.IsSuccess)
                                return JobOperationResult.Failure(
                                        JobOperationKind.PermanentFailure,
                                        JobApplicationErrors.StageRecoveryFailed);
                }

                Result transition = job.TransitionTo(
                        JobState.Completed,
                        _clock.UtcNow,
                        "Human review approved governed evidence.",
                        actor,
                        correlationId);
                if (transition.IsFailure)
                        return JobOperationResult.Failure(
                                JobOperationKind.InvalidTransition,
                                transition.Error);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return JobOperationResult.Success();
        }

        public async Task<JobOperationResult> ResolveArchitectureDecisionAsync(
                Guid jobId,
                ArchitectureDecisionRequest request,
                CancellationToken cancellationToken = default)
        {
                if (jobId == Guid.Empty || request is null ||
                    string.IsNullOrWhiteSpace(request.Actor) || request.Actor.Length > 200 ||
                    string.IsNullOrWhiteSpace(request.CorrelationId) || request.CorrelationId.Length > 200 ||
                    string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 4000 ||
                    request.Actor.IndexOfAny(['\0', '\r', '\n']) >= 0 ||
                    request.CorrelationId.IndexOfAny(['\0', '\r', '\n']) >= 0 ||
                    request.Reason.IndexOf('\0') >= 0 ||
                    request.FollowUpDescription?.IndexOf('\0') >= 0 ||
                    request.FollowUpDescription?.Length > 1000 ||
                    !Enum.IsDefined(request.Decision) ||
                    request.Decision == ArchitectureDecisionKind.AuthorizeScopeExpansion ||
                    (request.FollowUpRequired && string.IsNullOrWhiteSpace(request.FollowUpDescription)))
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.ArchitectureDecisionResolutionFailed);

                Job? job = await _jobRepository.GetByIdAsync(jobId, cancellationToken);
                if (job is null)
                        return JobOperationResult.Failure(JobOperationKind.PermanentFailure, JobApplicationErrors.NotFound);
                if (_artifactStore is null)
                        return JobOperationResult.Failure(JobOperationKind.PermanentFailure,
                                JobApplicationErrors.ArchitectureDecisionResolutionFailed);

                Guid runId = _jobRunIdProvider.Create(job.Id, job.AttemptCount);
                StageRecoveryResult existing = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.ArchitectureDecision,
                        request.CorrelationId, cancellationToken);
                JobOperationResult? existingFailure = RecoveryFailure(existing);
                if (existingFailure is not null) return existingFailure;
                if (existing.IsCompleted)
                {
                        if (existing.ArchitectureDecision is null ||
                            !ArchitectureDecisionMatches(existing.ArchitectureDecision, request))
                                return JobOperationResult.Failure(JobOperationKind.PermanentFailure,
                                        JobApplicationErrors.ArchitectureDecisionResolutionFailed);
                        return job.State == JobState.Completed
                                ? JobOperationResult.Success()
                                : JobOperationResult.Failure(JobOperationKind.InvalidTransition,
                                        JobApplicationErrors.ArchitectureDecisionResolutionFailed);
                }

                if (job.State != JobState.WaitingHuman)
                        return JobOperationResult.Failure(JobOperationKind.InvalidTransition,
                                JobApplicationErrors.ArchitectureDecisionResolutionFailed);

                StageRecoveryResult analysisResult = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.DevelopmentAnalysis,
                        request.CorrelationId, cancellationToken);
                JobOperationResult? analysisFailure = RecoveryFailure(analysisResult);
                if (analysisFailure is not null) return analysisFailure;
                DevelopmentAnalysis? analysis = analysisResult.DevelopmentAnalysis;
                ArtifactRecord? analysisArtifact = analysisResult.DevelopmentAnalysisArtifact;
                if (!analysisResult.IsCompleted || analysis is null || analysisArtifact is null ||
                    !analysis.ArchitectureDecisionRequired || analysis.DeveloperExecutionAllowed ||
                    analysis.JobId != job.Id || analysis.RunId != runId ||
                    analysis.AttemptCount != job.AttemptCount)
                        return JobOperationResult.Failure(JobOperationKind.PermanentFailure,
                                JobApplicationErrors.ArchitectureDecisionResolutionFailed);

                StageRecoveryResult planning = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.Planning, request.CorrelationId, cancellationToken);
                StageRecoveryResult developer = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.Developer, request.CorrelationId, cancellationToken);
                StageRecoveryResult observed = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.ObservedChanges, request.CorrelationId, cancellationToken);
                JobOperationResult? mutationEvidenceFailure = RecoveryFailure(planning) ??
                        RecoveryFailure(developer) ?? RecoveryFailure(observed);
                if (mutationEvidenceFailure is not null) return mutationEvidenceFailure;
                if (planning.IsCompleted || developer.IsCompleted || observed.IsCompleted)
                        return JobOperationResult.Failure(JobOperationKind.PermanentFailure,
                                JobApplicationErrors.ArchitectureDecisionResolutionFailed);

                var evidence = new ArchitectureDecisionEvidence
                {
                        JobId = job.Id, RunId = runId, AttemptCount = job.AttemptCount,
                        Actor = request.Actor, CorrelationId = request.CorrelationId,
                        Decision = request.Decision, Reason = request.Reason.Trim(),
                        DevelopmentAnalysisArtifactReference = analysisArtifact.RelativePath,
                        DevelopmentAnalysisSha256 = analysisArtifact.Sha256,
                        Classification = analysis.PrimaryClassification,
                        RequestedScope = analysis.RequestedScope,
                        RequiredScope = analysis.RequiredScope,
                        ImpactedLayers = analysis.ImpactedLayers,
                        SourceMutation = false,
                        FollowUpRequired = request.FollowUpRequired,
                        FollowUpDescription = request.FollowUpDescription?.Trim(),
                        RecordedAtUtc = _clock.UtcNow
                };
                ArtifactWriteResult write = await _artifactStore.WriteAsync(
                        new ArtifactWriteRequest
                        {
                                JobId = job.Id, RunId = runId,
                                ArtifactType = ArtifactType.ArchitectureDecision,
                                Content = JsonSerializer.SerializeToUtf8Bytes(evidence),
                                CorrelationId = request.CorrelationId
                        }, cancellationToken);
                if (!write.IsSuccess)
                        return JobOperationResult.Failure(JobOperationKind.PermanentFailure,
                                JobApplicationErrors.ArchitectureDecisionResolutionFailed);

                Result transition = job.TransitionTo(
                        JobState.Completed, _clock.UtcNow,
                        $"Architecture decision resolved without source mutation: {request.Decision}.",
                        request.Actor, request.CorrelationId);
                if (transition.IsFailure)
                        return JobOperationResult.Failure(JobOperationKind.InvalidTransition, transition.Error);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return JobOperationResult.Success();
        }

        private static bool ArchitectureDecisionMatches(
                ArchitectureDecisionEvidence evidence,
                ArchitectureDecisionRequest request) =>
                evidence.Actor == request.Actor &&
                evidence.CorrelationId == request.CorrelationId &&
                evidence.Decision == request.Decision &&
                evidence.Reason == request.Reason.Trim() &&
                evidence.FollowUpRequired == request.FollowUpRequired &&
                evidence.FollowUpDescription == request.FollowUpDescription?.Trim() &&
                !evidence.SourceMutation;

        public async Task<JobOperationResult> ApplyGovernedHumanCorrectionAsync(
                Guid jobId,
                GovernedHumanCorrectionRequest request,
                CancellationToken cancellationToken = default)
        {
                if (!ValidGovernedHumanCorrectionRequest(request) ||
                    _artifactStore is null ||
                    _artifactMetadataRepository is null ||
                    _developerProposalPolicy is null)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.StageRecoveryFailed);

                Job? job = await _jobRepository.GetByIdAsync(
                        jobId, cancellationToken);
                if (job is null)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.NotFound);

                Guid runId = _jobRunIdProvider.Create(
                        job.Id, job.AttemptCount);
                string requestSha =
                        GovernedHumanCorrectionPolicy.Fingerprint(request);
                StageRecoveryResult existing = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.GovernedHumanCorrection,
                        request.CorrelationId, cancellationToken);
                if (existing.IsCompleted)
                {
                        if (!string.Equals(
                                existing.GovernedHumanCorrection?.RequestSha256,
                                requestSha,
                                StringComparison.Ordinal))
                                return JobOperationResult.Failure(
                                        JobOperationKind.PermanentFailure,
                                        JobApplicationErrors.StageRecoveryFailed);
                        return await ResumeGovernedHumanCorrectionAsync(
                                job,
                                runId,
                                request,
                                existing,
                                cancellationToken);
                }
                if (existing.Status != StageRecoveryStatus.NotCompleted)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.StageRecoveryFailed);

                if (job.State != JobState.Building || job.IsTerminal)
                        return JobOperationResult.Failure(
                                JobOperationKind.InvalidTransition,
                                JobApplicationErrors.StageRecoveryFailed);

                StageRecoveryResult plan = await CheckRecoveryEvidenceAsync(
                        job, _jobRunIdProvider.Create(job.Id, 1),
                        RecoveryStage.Planning,
                        request.CorrelationId,
                        cancellationToken);
                StageRecoveryResult failedBuild = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.BuildOriginalFailure,
                        request.CorrelationId, cancellationToken);
                JobOperationResult? recoveryFailure =
                        RecoveryFailure(plan);
                if (recoveryFailure is not null)
                        return recoveryFailure;
                if (!plan.IsCompleted || plan.PlannerPlan is null ||
                    failedBuild.Status != StageRecoveryStatus.FailedExecution ||
                    failedBuild.BuildReport is null)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.StageRecoveryFailed);

                IReadOnlyList<ArtifactRecord> artifacts =
                        await _artifactMetadataRepository.GetByJobAndRunAsync(
                                job.Id, runId, cancellationToken);
                ArtifactRecord[] exhausted = artifacts.Where(artifact =>
                        artifact.ArtifactType is
                                ArtifactType.DeveloperBuildCorrectionRetryRejectedResponse or
                                ArtifactType.DeveloperBuildCorrectionRetryRejectedStructuredResponse)
                        .ToArray();

                ArtifactRecord? latestHumanCorrection = artifacts
                        .Where(artifact => artifact.ArtifactType ==
                                ArtifactType.GovernedHumanCorrectionRequest)
                        .OrderByDescending(artifact => artifact.CreatedAtUtc)
                        .ThenByDescending(artifact => artifact.ArtifactId)
                        .FirstOrDefault();

                StageRecoveryResult appliedCorrection =
                        await CheckRecoveryEvidenceAsync(
                                job, runId,
                                RecoveryStage.DeveloperBuildCorrection,
                                request.CorrelationId, cancellationToken);
                StageRecoveryResult observedCorrection =
                        await CheckRecoveryEvidenceAsync(
                                job, runId,
                                RecoveryStage.ObservedBuildCorrection,
                                request.CorrelationId, cancellationToken);
                StageRecoveryResult failedCorrectedBuild =
                        await CheckRecoveryEvidenceAsync(
                                job, runId,
                                RecoveryStage.BuildCorrectionFailure,
                                request.CorrelationId, cancellationToken);
                bool failedCorrectedRebuildExhausted =
                        appliedCorrection.IsCompleted &&
                        observedCorrection.IsCompleted &&
                        failedCorrectedBuild.Status ==
                                StageRecoveryStatus.FailedExecution &&
                        GovernedHumanCorrectionPolicy
                                .IsFailedCorrectedRebuildExhaustion(
                                        appliedCorrection.DeveloperProposal,
                                        observedCorrection.ObservedChangeManifest,
                                        failedCorrectedBuild.BuildReport);

                bool sequentialCorrectionEligible = false;
                if (latestHumanCorrection is not null)
                {
                        string latestCorrelationId =
                                latestHumanCorrection.CorrelationId;
                        StageRecoveryResult latestCorrection =
                                await CheckRecoveryEvidenceAsync(
                                        job, runId,
                                        RecoveryStage.GovernedHumanCorrection,
                                        latestCorrelationId,
                                        cancellationToken);
                        StageRecoveryResult latestObserved =
                                await CheckRecoveryEvidenceAsync(
                                        job, runId,
                                        RecoveryStage.ObservedGovernedHumanCorrection,
                                        latestCorrelationId,
                                        cancellationToken);
                        StageRecoveryResult latestFailedBuild =
                                await CheckRecoveryEvidenceAsync(
                                        job, runId,
                                        RecoveryStage.BuildGovernedHumanCorrectionFailure,
                                        latestCorrelationId,
                                        cancellationToken);
                        sequentialCorrectionEligible =
                                GovernedHumanCorrectionPolicy
                                        .IsSequentialCorrectionEligible(
                                                job.State,
                                                job.IsTerminal,
                                                latestCorrection.IsCompleted,
                                                latestObserved.IsCompleted,
                                                latestFailedBuild.Status ==
                                                        StageRecoveryStatus.FailedExecution);
                }

                bool firstCorrectionEligible =
                        GovernedHumanCorrectionPolicy.IsEligible(
                                job.State,
                                job.IsTerminal,
                                hasValidFailureEvidence:
                                        failedBuild.Status ==
                                        StageRecoveryStatus.FailedExecution,
                                rejectedAiCorrectionExhausted:
                                        exhausted.Length > 0,
                                failedCorrectedRebuildExhausted,
                                plan.PlannerPlan.CandidateFilesToModify);
                if (latestHumanCorrection is null
                        ? !firstCorrectionEligible
                        : !sequentialCorrectionEligible)
                        return JobOperationResult.Failure(
                                JobOperationKind.InvalidTransition,
                                JobApplicationErrors.StageRecoveryFailed);

                HashSet<string> allowlist =
                        plan.PlannerPlan.CandidateFilesToModify
                                .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!GovernedHumanCorrectionPolicy.IsValidRequest(
                        request,
                        plan.PlannerPlan.CandidateFilesToModify))
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.StageRecoveryFailed);

                var proposal = new DeveloperProposal
                {
                        Summary = string.IsNullOrWhiteSpace(request.Reason) ||
                                  request.Reason.Length > 80
                                ? "Apply governed human correction."
                                : request.Reason,
                        Changes = request.Changes.Select(change =>
                                new DeveloperChangeOperation
                                {
                                        Operation =
                                                DeveloperChangeOperationType.ReplaceFile,
                                        RelativePath = change.RelativePath,
                                        Intent = "Apply authorized human replacement.",
                                        Content = change.Content,
                                        ExpectedContentSha256 =
                                                change.ExpectedContentSha256
                                }).ToArray(),
                        Assumptions = [],
                        Risks = []
                };
                DeveloperProposalPolicyResult policy =
                        _developerProposalPolicy.Validate(proposal);
                if (!policy.IsSuccess || policy.Proposal is null ||
                    BuildCorrectionNoOpPolicy.NoOpPaths(policy.Proposal).Count > 0)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.StageRecoveryFailed);

                ExecutionPlaneLifecycleResult recovery =
                        await _executionPlaneLifecycle.RecoverAsync(
                                job.Id, job.ExternalId, cancellationToken);
                if (!recovery.IsSuccess || recovery.Session is null ||
                    recovery.Session.Repository.JobId != job.Id ||
                    !string.Equals(
                            recovery.Session.Repository.Head,
                            job.BaseRepositoryHead,
                            StringComparison.OrdinalIgnoreCase))
                        return JobOperationResult.Failure(
                                JobOperationKind.RetryableFailure,
                                JobApplicationErrors.ExecutionPlanePreparationFailed);

                var evidence = new GovernedHumanCorrectionEvidence
                {
                        JobId = job.Id,
                        RunId = runId,
                        AttemptCount = job.AttemptCount,
                        Stage = job.State.ToString(),
                        Actor = request.Actor,
                        CorrelationId = request.CorrelationId,
                        Reason = request.Reason,
                        RequestSha256 = requestSha,
                        FailureEvidence = artifacts.Where(artifact =>
                                artifact.ArtifactType is
                                        ArtifactType.BuildReport or
                                        ArtifactType.BuildCorrectionReport or
                                        ArtifactType.BuildCorrectionRetryReport or
                                        ArtifactType.BuildHumanReviewCorrectionReport or
                                        ArtifactType.BuildGovernedHumanCorrectionReport)
                                .Select(artifact => artifact.RelativePath)
                                .ToArray(),
                        ExhaustedAiCorrectionEvidence = exhausted
                                .Select(artifact => artifact.RelativePath)
                                .ToArray(),
                        AutomaticCorrectionExhaustionEvidence =
                                failedCorrectedRebuildExhausted
                                        ? artifacts.Where(artifact =>
                                                artifact.ArtifactType is
                                                        ArtifactType.DeveloperBuildCorrectionProposal or
                                                        ArtifactType.ObservedBuildCorrectionManifest or
                                                        ArtifactType.BuildCorrectionReport)
                                                .Select(artifact => artifact.RelativePath)
                                                .ToArray()
                                        : [],
                        AllowedPaths = plan.PlannerPlan.CandidateFilesToModify,
                        Proposal = policy.Proposal,
                        RecordedAtUtc = _clock.UtcNow
                };
                ArtifactWriteResult requestWrite =
                        await _artifactStore.WriteAsync(
                                new ArtifactWriteRequest
                                {
                                        JobId = job.Id,
                                        RunId = runId,
                                        ArtifactType =
                                                ArtifactType.GovernedHumanCorrectionRequest,
                                        Content = JsonSerializer.SerializeToUtf8Bytes(evidence),
                                        CorrelationId = request.CorrelationId
                                },
                                cancellationToken);
                if (!requestWrite.IsSuccess)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.StageRecoveryFailed);

                SafeChangeApplicationResult safeChange =
                        await _safeChangeApplier.ApplyAsync(
                                new SafeChangeApplicationRequest
                                {
                                        JobId = job.Id,
                                        RunId = runId,
                                        AttemptCount = job.AttemptCount,
                                        Repository = recovery.Session.Repository,
                                        Proposal = policy.Proposal,
                                        CorrelationId = request.CorrelationId,
                                        ProposalLineageId =
                                                $"governed-human-correction:{request.CorrelationId}",
                                        IsGovernedHumanCorrection = true
                                },
                                cancellationToken);
                if (!safeChange.IsSuccess || safeChange.Report is null)
                        return JobOperationResult.Failure(
                                MapSafeChangeFailureKind(safeChange.FailureKind),
                                JobApplicationErrors.SafeChangeApplicationFailed);

                var receipt = new GovernedHumanCorrectionReceipt
                {
                        JobId = job.Id,
                        RunId = runId,
                        AttemptCount = job.AttemptCount,
                        Actor = request.Actor,
                        CorrelationId = request.CorrelationId,
                        RequestSha256 = requestSha,
                        Changes = safeChange.Report.Changes,
                        RecordedAtUtc = _clock.UtcNow
                };
                ArtifactWriteResult receiptWrite =
                        await _artifactStore.WriteAsync(
                                new ArtifactWriteRequest
                                {
                                        JobId = job.Id,
                                        RunId = runId,
                                        ArtifactType =
                                                ArtifactType.GovernedHumanCorrectionReceipt,
                                        Content = JsonSerializer.SerializeToUtf8Bytes(receipt),
                                        CorrelationId = request.CorrelationId
                                },
                                cancellationToken);
                if (!receiptWrite.IsSuccess)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.StageRecoveryFailed);

                ObservedChangeEvidenceResult observed =
                        await _observedChangeEvidenceService.CaptureAsync(
                                new ObservedChangeEvidenceRequest
                                {
                                        JobId = job.Id,
                                        RunId = runId,
                                        Repository = recovery.Session.Repository,
                                        Proposal = policy.Proposal,
                                        CorrelationId = request.CorrelationId,
                                        AttemptCount = job.AttemptCount,
                                        ProposalLineageId =
                                                $"governed-human-correction:{request.CorrelationId}",
                                        ProposalFingerprintSha256 =
                                                SafeChangeProposalIdentity.Fingerprint(
                                                        policy.Proposal),
                                        SafeChangeReceiptReference =
                                                receiptWrite.Artifact?.RelativePath ??
                                                $"governed-human-correction:{request.CorrelationId}",
                                        SafeChangeChanges = receipt.Changes,
                                        IsGovernedHumanCorrection = true,
                                        AllowedPaths =
                                                plan.PlannerPlan.CandidateFilesToModify
                                },
                                cancellationToken);
                if (!observed.IsSuccess)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.ObservedChangeEvidenceInvalid);

                BuildExecutionResult build =
                        await _buildExecutionService.ExecuteAsync(
                                new BuildExecutionRequest
                                {
                                        JobId = job.Id,
                                        RunId = runId,
                                        Repository = recovery.Session.Repository,
                                        Target = _executionTargetProvider.DotnetTarget,
                                        CorrelationId = request.CorrelationId,
                                        IsGovernedHumanCorrection = true
                                },
                                cancellationToken);
                if (!build.IsSuccess)
                        return JobOperationResult.Failure(
                                MapBuildFailureKind(build.FailureKind),
                                JobApplicationErrors.BuildExecutionFailed);

                return await TransitionAndSaveAsync(
                        job,
                        JobState.Testing,
                        "Governed human correction applied; build passed.",
                        request.Actor,
                        request.CorrelationId,
                        cancellationToken);
        }

        private async Task<JobOperationResult>
                ResumeGovernedHumanCorrectionAsync(
                        Job job,
                        Guid runId,
                        GovernedHumanCorrectionRequest request,
                        StageRecoveryResult recoveredCorrection,
                        CancellationToken cancellationToken)
        {
                if (job.State != JobState.Building ||
                    job.IsTerminal ||
                    recoveredCorrection.GovernedHumanCorrection is null ||
                    recoveredCorrection.GovernedHumanCorrectionReceipt is null ||
                    recoveredCorrection.DeveloperProposal is null)
                        return JobOperationResult.Failure(
                                JobOperationKind.InvalidTransition,
                                JobApplicationErrors.StageRecoveryFailed);

                StageRecoveryResult plan = await CheckRecoveryEvidenceAsync(
                        job,
                        _jobRunIdProvider.Create(job.Id, 1),
                        RecoveryStage.Planning,
                        request.CorrelationId,
                        cancellationToken);
                JobOperationResult? planFailure = RecoveryFailure(plan);
                if (planFailure is not null)
                        return planFailure;
                if (!plan.IsCompleted || plan.PlannerPlan is null)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.StageRecoveryFailed);

                ExecutionPlaneLifecycleResult recovery =
                        await _executionPlaneLifecycle.RecoverAsync(
                                job.Id,
                                job.ExternalId,
                                cancellationToken);
                if (!recovery.IsSuccess ||
                    recovery.Session is null ||
                    recovery.Session.Repository.JobId != job.Id ||
                    !string.Equals(
                            recovery.Session.Repository.Head,
                            job.BaseRepositoryHead,
                            StringComparison.OrdinalIgnoreCase))
                        return JobOperationResult.Failure(
                                JobOperationKind.RetryableFailure,
                                JobApplicationErrors.ExecutionPlanePreparationFailed);

                string proposalLineageId =
                        $"governed-human-correction:{request.CorrelationId}";
                StageRecoveryResult observed = await CheckRecoveryEvidenceAsync(
                        job,
                        runId,
                        RecoveryStage.ObservedGovernedHumanCorrection,
                        request.CorrelationId,
                        cancellationToken);
                JobOperationResult? observedFailure = RecoveryFailure(observed);
                if (observedFailure is not null)
                        return observedFailure;
                if (!observed.IsCompleted)
                {
                        ObservedChangeEvidenceResult captured =
                                await _observedChangeEvidenceService.CaptureAsync(
                                        new ObservedChangeEvidenceRequest
                                        {
                                                JobId = job.Id,
                                                RunId = runId,
                                                AttemptCount = job.AttemptCount,
                                                Repository = recovery.Session.Repository,
                                                Proposal = recoveredCorrection.DeveloperProposal,
                                                CorrelationId = request.CorrelationId,
                                                ProposalLineageId = proposalLineageId,
                                                ProposalFingerprintSha256 =
                                                        SafeChangeProposalIdentity.Fingerprint(
                                                                recoveredCorrection.DeveloperProposal),
                                                SafeChangeReceiptReference =
                                                        proposalLineageId,
                                                SafeChangeChanges =
                                                        recoveredCorrection
                                                                .GovernedHumanCorrectionReceipt
                                                                .Changes,
                                                IsGovernedHumanCorrection = true,
                                                AllowedPaths =
                                                        plan.PlannerPlan
                                                                .CandidateFilesToModify
                                        },
                                        cancellationToken);
                        if (!captured.IsSuccess)
                                return JobOperationResult.Failure(
                                        JobOperationKind.PermanentFailure,
                                        JobApplicationErrors
                                                .ObservedChangeEvidenceInvalid);
                }

                StageRecoveryResult recoveredBuild =
                        await CheckRecoveryEvidenceAsync(
                                job,
                                runId,
                                RecoveryStage.BuildGovernedHumanCorrection,
                                request.CorrelationId,
                                cancellationToken);
                JobOperationResult? buildRecoveryFailure =
                        RecoveryFailure(recoveredBuild);
                if (buildRecoveryFailure is not null)
                        return buildRecoveryFailure;
                if (!recoveredBuild.IsCompleted)
                {
                        BuildExecutionResult build =
                                await _buildExecutionService.ExecuteAsync(
                                        new BuildExecutionRequest
                                        {
                                                JobId = job.Id,
                                                RunId = runId,
                                                Repository = recovery.Session.Repository,
                                                Target =
                                                        _executionTargetProvider.DotnetTarget,
                                                CorrelationId = request.CorrelationId,
                                                IsGovernedHumanCorrection = true
                                        },
                                        cancellationToken);
                        if (!build.IsSuccess)
                                return JobOperationResult.Failure(
                                        MapBuildFailureKind(build.FailureKind),
                                        JobApplicationErrors.BuildExecutionFailed);
                }

                return await TransitionAndSaveAsync(
                        job,
                        JobState.Testing,
                        "Governed human correction evidence recovered; observed changes and build passed.",
                        request.Actor,
                        request.CorrelationId,
                        cancellationToken);
        }

        public async Task<JobOperationResult>
                SupersedeHumanReviewCorrectionReviewAsync(
                        Guid jobId,
                        string actor,
                        string correlationId,
                        CancellationToken cancellationToken = default)
        {
                if (jobId == Guid.Empty ||
                    string.IsNullOrWhiteSpace(actor) ||
                    string.IsNullOrWhiteSpace(correlationId))
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.StageRecoveryFailed);

                Job? job = await _jobRepository.GetByIdAsync(
                        jobId, cancellationToken);
                if (job is null)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.NotFound);
                if (job.State != JobState.WaitingHuman)
                        return JobOperationResult.Failure(
                                JobOperationKind.InvalidTransition,
                                JobApplicationErrors.StageRecoveryFailed);

                Guid runId = _jobRunIdProvider.Create(
                        job.Id, job.AttemptCount);
                StageRecoveryResult superseding =
                        await CheckRecoveryEvidenceAsync(
                                job, runId,
                                RecoveryStage.ReviewerHumanReviewCorrectionSourceAwareSuperseding,
                                correlationId, cancellationToken);
                JobOperationResult? supersedingFailure =
                        RecoveryFailure(superseding);
                if (supersedingFailure is not null)
                        return supersedingFailure;
                if (superseding.IsCompleted)
                        return JobOperationResult.Success();

                StageRecoveryResult priorReview =
                        await CheckRecoveryEvidenceAsync(
                                job, runId,
                                RecoveryStage.ReviewerHumanReviewCorrection,
                                correlationId, cancellationToken);
                StageRecoveryResult humanCorrection =
                        await CheckRecoveryEvidenceAsync(
                                job, runId,
                                RecoveryStage.HumanReviewCorrection,
                                correlationId, cancellationToken);
                StageRecoveryResult plan =
                        await CheckRecoveryEvidenceAsync(
                                job, _jobRunIdProvider.Create(job.Id, 1),
                                RecoveryStage.Planning,
                                correlationId, cancellationToken);
                StageRecoveryResult developer =
                        await CheckRecoveryEvidenceAsync(
                                job, runId,
                                RecoveryStage.EffectiveDeveloperProposal,
                                correlationId, cancellationToken);
                StageRecoveryResult observed =
                        await CheckRecoveryEvidenceAsync(
                                job, runId,
                                RecoveryStage.ObservedHumanReviewCorrection,
                                correlationId, cancellationToken);
                StageRecoveryResult build =
                        await CheckRecoveryEvidenceAsync(
                                job, runId,
                                RecoveryStage.BuildHumanReviewCorrection,
                                correlationId, cancellationToken);
                StageRecoveryResult test =
                        await CheckRecoveryEvidenceAsync(
                                job, runId,
                                RecoveryStage.TestHumanReviewCorrection,
                                correlationId, cancellationToken);

                foreach (StageRecoveryResult evidence in new[]
                {
                        priorReview, humanCorrection, plan,
                        developer, observed, build, test
                })
                {
                        JobOperationResult? failure = RecoveryFailure(evidence);
                        if (failure is not null) return failure;
                        if (!evidence.IsCompleted)
                                return JobOperationResult.Failure(
                                        JobOperationKind.PermanentFailure,
                                        JobApplicationErrors.ReviewerEvidenceInvalid);
                }

                if (priorReview.ReviewerReview?.Decision ==
                        ReviewerDecision.Approved ||
                    humanCorrection.HumanReviewCorrection is null ||
                    plan.PlannerPlan is null ||
                    developer.DeveloperProposal is null ||
                    developer.DeveloperProposalLineage !=
                        DeveloperProposalLineage.HumanReviewCorrection ||
                    observed.ObservedChangeManifest is null ||
                    build.BuildReport is null ||
                    test.TestReport is null)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.ReviewerEvidenceInvalid);

                ReviewerEffectiveSourceSnapshot? effectiveSource =
                        await CaptureReviewerEffectiveSourceAsync(
                                job,
                                runId,
                                plan.PlannerPlan,
                                developer.DeveloperProposal,
                                observed.ObservedChangeManifest,
                                developer.DeveloperProposalLineage.Value,
                                cancellationToken);
                if (effectiveSource is null)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.ReviewerEvidenceInvalid);

                ReviewerExecutionResult executed =
                        await _reviewerExecutionService.ExecuteAsync(
                                new ReviewerExecutionRequest
                                {
                                        JobId = job.Id,
                                        RunId = runId,
                                        JobRequest = job.Request,
                                        Plan = plan.PlannerPlan,
                                        DeveloperProposal =
                                                developer.DeveloperProposal,
                                        EffectiveProposalLineage =
                                                developer.DeveloperProposalLineage.Value,
                                        EffectiveSourceSnapshot = effectiveSource,
                                        ObservedChanges =
                                                observed.ObservedChangeManifest,
                                        BuildReport = build.BuildReport,
                                        TestReport = test.TestReport,
                                        HumanReviewCorrection =
                                                humanCorrection.HumanReviewCorrection,
                                        IsSupersedingHumanReviewCorrection = true,
                                        SupersedingReviewVersion = 3,
                                        CorrelationId = correlationId
                                },
                                cancellationToken);
                if (!executed.IsSuccess || executed.Review is null)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.ReviewerExecutionFailed);

                ReviewDecisionResult decision = _reviewDecisionPolicy.Evaluate(
                        new ReviewDecisionInput(
                                job.Id,
                                runId,
                                executed.Review,
                                build.BuildReport,
                                test.TestReport,
                                observed.ObservedChangeManifest));
                return decision.IsSuccess && decision.Decision.HasValue
                        ? JobOperationResult.Success()
                        : JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.ReviewDecisionFailed);
        }

        private static bool ValidHumanReviewRequest(HumanReviewCorrectionRequest? request)
        {
                if (request is null || string.IsNullOrWhiteSpace(request.Actor) ||
                    string.IsNullOrWhiteSpace(request.CorrelationId) ||
                    request.RequiredCorrections is not { Count: > 0 and <= 20 })
                        return false;

                return request.RequiredCorrections.All(correction =>
                        IsSafeRelativePath(correction.RelativePath) &&
                        !string.IsNullOrWhiteSpace(correction.Instruction) &&
                        correction.Instruction.Length <= 4000);
        }

        private static bool ValidGovernedHumanCorrectionRequest(
                GovernedHumanCorrectionRequest? request)
        {
                if (request is null ||
                    string.IsNullOrWhiteSpace(request.Actor) ||
                    string.IsNullOrWhiteSpace(request.CorrelationId) ||
                    request.Actor.Length > 200 ||
                    request.CorrelationId.Length > 200 ||
                    request.Reason.Length > 4000 ||
                    request.Actor.IndexOfAny(['\0', '\r', '\n']) >= 0 ||
                    request.CorrelationId.IndexOfAny(['\0', '\r', '\n']) >= 0 ||
                    request.Changes is not { Count: > 0 and <= 4 })
                        return false;

                string[] paths = request.Changes
                        .Select(change => change.RelativePath)
                        .ToArray();
                return paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() ==
                                paths.Length &&
                        request.Changes.All(change =>
                                IsSafeRelativePath(change.RelativePath) &&
                                change.ExpectedContentSha256 is { Length: 64 } &&
                                change.ExpectedContentSha256.All(character =>
                                        character is >= '0' and <= '9' or
                                                >= 'a' and <= 'f') &&
                                !string.IsNullOrEmpty(change.Content));
        }

        private static bool IsSafeRelativePath(string? path)
        {
                if (string.IsNullOrWhiteSpace(path) || path.Length > 512) return false;
                string normalized = path.Replace('\\', '/');
                return !normalized.StartsWith('/') && !normalized.Contains(':') &&
                    !normalized.Contains('\0') && normalized.Split('/').All(segment =>
                        segment.Length > 0 && segment is not "." and not "..");
        }

        public JobState? DetermineNextState(
                JobState currentState)
        {
                return _stateMachine
                        .DetermineNextAutomaticState(
                                currentState);
        }

        public async Task<JobOperationResult> AdvanceAsync(
                Guid jobId,
                string actor,
                string correlationId,
                CancellationToken cancellationToken =
                        default(CancellationToken))
        {
                Job? job =
                        await _jobRepository
                                .GetByIdAsync(
                                        jobId,
                                        cancellationToken);

                if (job is null)
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.NotFound);
                }

                if (_stateMachine.IsTerminal(job.State))
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.InvalidTransition,
                                JobApplicationErrors.TerminalJob);
                }

                if (job.HasTimedOut(_clock.UtcNow))
                {
                        Result timeout =
                                job.TransitionTo(
                                        JobState.TimedOut,
                                        _clock.UtcNow,
                                        "Maximum job duration exceeded.",
                                        actor,
                                        correlationId);

                        if (timeout.IsFailure)
                        {
                                return JobOperationResult.Failure(
                                        JobOperationKind.OperationalFailure,
                                        timeout.Error);
                        }

                        await _unitOfWork
                                .SaveChangesAsync(
                                        cancellationToken);

                        return JobOperationResult.Failure(
                                JobOperationKind.TimedOut,
                                JobApplicationErrors.TimedOut);
                }

                job.BeginActiveExecution(_clock.UtcNow);

                await _unitOfWork
                        .SaveChangesAsync(
                                cancellationToken);

                JobOperationResult result;

                try
                {
                        result = await AdvanceActiveStateAsync(
                                job,
                                actor,
                                correlationId,
                                cancellationToken);
                }
                catch
                {
                        job.CompleteActiveExecution(_clock.UtcNow);

                        await _unitOfWork
                                .SaveChangesAsync(
                                        CancellationToken.None);

                        throw;
                }

                if (job.HasTimedOut(_clock.UtcNow))
                {
                        Result timeout =
                                job.TransitionTo(
                                        JobState.TimedOut,
                                        _clock.UtcNow,
                                        "Maximum active execution duration exceeded.",
                                        actor,
                                        correlationId);

                        if (timeout.IsFailure)
                        {
                                return JobOperationResult.Failure(
                                        JobOperationKind.OperationalFailure,
                                        timeout.Error);
                        }

                        await _unitOfWork
                                .SaveChangesAsync(
                                        cancellationToken);

                        return JobOperationResult.Failure(
                                JobOperationKind.TimedOut,
                                JobApplicationErrors.TimedOut);
                }

                job.CompleteActiveExecution(_clock.UtcNow);

                await _unitOfWork
                        .SaveChangesAsync(
                                cancellationToken);

                return result;
        }

        private async Task<JobOperationResult> AdvanceActiveStateAsync(
                Job job,
                string actor,
                string correlationId,
                CancellationToken cancellationToken)
        {

                if (job.State == JobState.ContextBuilding)
                {
                        return await AdvanceContextBuildingAsync(
                                job,
                                actor,
                                correlationId,
                                cancellationToken);
                }

                if (job.State == JobState.Planning)
                {
                        return await AdvancePlanningAsync(
                                job,
                                actor,
                                correlationId,
                                cancellationToken);
                }

                if (job.State ==
                    JobState.WorkspacePreparing)
                {
                        return await AdvanceWorkspacePreparingAsync(
                                job,
                                actor,
                                correlationId,
                                cancellationToken);
                }

                if (job.State == JobState.Developing)
                {
                        return await AdvanceDevelopingAsync(
                                job,
                                actor,
                                correlationId,
                                cancellationToken);
                }

                if (job.State == JobState.Building)
                {
                        return await AdvanceBuildingAsync(
                                job,
                                actor,
                                correlationId,
                                cancellationToken);
                }

                if (job.State == JobState.Testing)
                {
                        return await AdvanceTestingAsync(
                                job,
                                actor,
                                correlationId,
                                cancellationToken);
                }

                if (job.State == JobState.Reviewing)
                {
                        return await AdvanceReviewingAsync(job, actor, correlationId, cancellationToken);
                }

                return await AdvanceStateOnlyAsync(
                        job,
                        actor,
                        correlationId,
                        cancellationToken);
        }

        private async Task<JobOperationResult>
                AdvanceContextBuildingAsync(
                        Job job,
                        string actor,
                        string correlationId,
                        CancellationToken cancellationToken)
        {
                if (job.BaseRepositoryHead is null)
                {
                        SourceRevisionResult revision;

                        try
                        {
                                revision =
                                        await _sourceRevisionProvider
                                                .GetAuthoritativeHeadAsync(
                                                        cancellationToken);
                        }
                        catch (OperationCanceledException)
                                when (cancellationToken
                                        .IsCancellationRequested)
                        {
                                throw;
                        }
                        catch
                        {
                                return JobOperationResult.Failure(
                                        JobOperationKind.RetryableFailure,
                                        JobApplicationErrors
                                                .SourceRevisionUnavailable);
                        }

                        if (!revision.IsSuccess ||
                            revision.Head is null)
                        {
                                return JobOperationResult.Failure(
                                        JobOperationKind.RetryableFailure,
                                        JobApplicationErrors
                                                .SourceRevisionUnavailable);
                        }

                        Result pin =
                                job.PinBaseRepositoryHead(
                                        revision.Head);

                        if (pin.IsFailure)
                        {
                                return JobOperationResult.Failure(
                                        JobOperationKind.OperationalFailure,
                                        pin.Error);
                        }

                        // Persist the authoritative revision before
                        // any external workspace/context work begins.
                        await _unitOfWork
                                .SaveChangesAsync(
                                        cancellationToken);
                }

                if (string.IsNullOrWhiteSpace(
                        job.BaseRepositoryHead))
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.OperationalFailure,
                                JobApplicationErrors
                                        .MissingBaseRepositoryHead);
                }

                ExecutionPlaneLifecycleResult preparation;

                try
                {
                        preparation =
                                await _executionPlaneLifecycle
                                        .PrepareAsync(
                                                job.Id,
                                                job.ExternalId,
                                                job.BaseRepositoryHead,
                                                cancellationToken);
                }
                catch (OperationCanceledException)
                        when (cancellationToken
                                .IsCancellationRequested)
                {
                        throw;
                }
                catch
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.RetryableFailure,
                                JobApplicationErrors
                                        .ExecutionPlanePreparationFailed);
                }

                if (!preparation.IsSuccess ||
                    preparation.Session is null)
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.RetryableFailure,
                                JobApplicationErrors
                                        .ExecutionPlanePreparationFailed);
                }

                var repository =
                        preparation.Session.Repository;

                if (repository.JobId != job.Id ||
                    !string.Equals(
                            repository.JobExternalId,
                            job.ExternalId,
                            StringComparison.Ordinal) ||
                    !string.Equals(
                            repository.Head,
                            job.BaseRepositoryHead,
                            StringComparison.OrdinalIgnoreCase))
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors
                                        .ExecutionPlaneRepositoryMismatch);
                }

                Guid runId;

                try
                {
                        runId =
                                _jobRunIdProvider.Create(
                                        job.Id,
                                        job.AttemptCount);
                }
                catch
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.OperationalFailure,
                                JobApplicationErrors
                                        .ContextGenerationFailed);
                }

                StageRecoveryResult recoveredContext =
                        await CheckRecoveryEvidenceAsync(
                                job,
                                runId,
                                RecoveryStage.Context,
                                correlationId,
                                cancellationToken);

                if (recoveredContext.IsCompleted)
                {
                        return await RunDevelopmentAnalysisAsync(
                                job, runId, repository, actor, correlationId,
                                cancellationToken);
                }

                JobOperationResult? contextRecoveryFailure =
                        RecoveryFailure(
                                recoveredContext);

                if (contextRecoveryFailure is not null)
                {
                        return contextRecoveryFailure;
                }

                ContextGenerationResult context;

                try
                {
                        context =
                                await _contextGenerationService
                                        .GenerateAsync(
                                                new ContextGenerationRequest
                                                {
                                                        JobId =
                                                                job.Id,

                                                        RunId =
                                                                runId,

                                                        JobExternalId =
                                                                job.ExternalId,

                                                        Repository =
                                                                repository,

                                                        BaseRepositoryHead =
                                                                job.BaseRepositoryHead,

                                                        CorrelationId =
                                                                correlationId
                                                },
                                                cancellationToken);
                }
                catch (OperationCanceledException)
                        when (cancellationToken
                                .IsCancellationRequested)
                {
                        throw;
                }
                catch
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.RetryableFailure,
                                JobApplicationErrors
                                        .ContextGenerationFailed);
                }

                if (!context.IsSuccess)
                {
                        return JobOperationResult.Failure(
                                MapContextFailureKind(
                                        context.FailureKind),
                                JobApplicationErrors
                                        .ContextGenerationFailed);
                }

                return await RunDevelopmentAnalysisAsync(
                        job, runId, repository, actor, correlationId,
                        cancellationToken);
        }

        private async Task<JobOperationResult> RunDevelopmentAnalysisAsync(
                Job job,
                Guid runId,
                RepositoryWorktreeHandle repository,
                string actor,
                string correlationId,
                CancellationToken cancellationToken)
        {
                if (_developmentAnalysisService is null)
                {
                        return await TransitionAndSaveAsync(
                                job, JobState.Planning,
                                "Context package generated from the pinned repository revision.",
                                actor, correlationId, cancellationToken);
                }

                StageRecoveryResult recovered = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.DevelopmentAnalysis,
                        correlationId, cancellationToken);
                DevelopmentAnalysisResult? result = null;
                DevelopmentAnalysis? analysis = recovered.DevelopmentAnalysis;
                if (!recovered.IsCompleted)
                {
                        JobOperationResult? recoveryFailure = RecoveryFailure(recovered);
                        if (recoveryFailure is not null) return recoveryFailure;
                }

                try
                {
                        if (analysis is null) result = await _developmentAnalysisService.AnalyzeAsync(
                                new DevelopmentAnalysisRequest
                                {
                                        JobId = job.Id,
                                        RunId = runId,
                                        AttemptCount = job.AttemptCount,
                                        JobRequest = job.Request,
                                        Repository = repository,
                                        CorrelationId = correlationId
                                }, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                        throw;
                }
                catch
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.RetryableFailure,
                                JobApplicationErrors.DevelopmentAnalysisFailed);
                }

                if (analysis is null && (result is null || !result.IsSuccess || result.Analysis is null))
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.DevelopmentAnalysisFailed);
                }

                analysis ??= result!.Analysis!;
                if (analysis.PrimaryClassification ==
                    DevelopmentChangeClassification.AlreadySatisfied)
                {
                        return await TransitionAndSaveAsync(
                                job, JobState.Completed,
                                "Development analysis determined the request is already satisfied.",
                                actor, correlationId, cancellationToken);
                }

                if (!analysis.DeveloperExecutionAllowed)
                {
                        return await TransitionAndSaveAsync(
                                job, JobState.WaitingHuman,
                                "Development analysis requires an architecture decision.",
                                actor, correlationId, cancellationToken);
                }

                return await TransitionAndSaveAsync(
                        job, JobState.Planning,
                        "Context and development analysis completed against the pinned repository revision.",
                        actor, correlationId, cancellationToken);
        }

        private async Task<JobOperationResult>
                AdvancePlanningAsync(
                        Job job,
                        string actor,
                        string correlationId,
                        CancellationToken cancellationToken)
        {
                if (string.IsNullOrWhiteSpace(
                        job.BaseRepositoryHead))
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.OperationalFailure,
                                JobApplicationErrors
                                        .MissingBaseRepositoryHead);
                }

                Guid runId;

                try
                {
                        runId =
                                _jobRunIdProvider.Create(
                                        job.Id,
                                        job.AttemptCount);
                }
                catch
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.OperationalFailure,
                                JobApplicationErrors
                                        .PlanningExecutionFailed);
                }

                StageRecoveryResult recoveredPlanning =
                        await CheckRecoveryEvidenceAsync(
                                job,
                                runId,
                                RecoveryStage.Planning,
                                correlationId,
                                cancellationToken);

                if (recoveredPlanning.IsCompleted)
                {
                        return await TransitionAndSaveAsync(
                                job,
                                JobState.WorkspacePreparing,
                                "AI planning evidence recovered; inference was not repeated.",
                                actor,
                                correlationId,
                                cancellationToken);
                }

                JobOperationResult? planningRecoveryFailure =
                        RecoveryFailure(
                                recoveredPlanning);

                if (planningRecoveryFailure is not null)
                {
                        return planningRecoveryFailure;
                }

                PlanningExecutionResult planning;

                try
                {
                        planning =
                                await _planningExecutionService
                                        .ExecuteAsync(
                                                new PlanningExecutionRequest
                                                {
                                                        JobId =
                                                                job.Id,

                                                        RunId =
                                                                runId,

                                                        JobRequest =
                                                                job.Request,

                                                        CorrelationId =
                                                                correlationId
                                                },
                                                cancellationToken);
                }
                catch (OperationCanceledException)
                        when (cancellationToken
                                .IsCancellationRequested)
                {
                        throw;
                }
                catch
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.RetryableFailure,
                                JobApplicationErrors
                                        .PlanningExecutionFailed);
                }

                if (!planning.IsSuccess)
                {
                        return JobOperationResult.Failure(
                                MapPlanningFailureKind(
                                        planning.FailureKind),
                                JobApplicationErrors
                                        .PlanningExecutionFailed);
                }

                return await TransitionAndSaveAsync(
                        job,
                        JobState.WorkspacePreparing,
                        "AI planning completed and response evidence was persisted.",
                        actor,
                        correlationId,
                        cancellationToken);
        }

        private async Task<JobOperationResult>
                AdvanceWorkspacePreparingAsync(
                        Job job,
                        string actor,
                        string correlationId,
                        CancellationToken cancellationToken)
        {
                if (string.IsNullOrWhiteSpace(
                        job.BaseRepositoryHead))
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.OperationalFailure,
                                JobApplicationErrors
                                        .MissingBaseRepositoryHead);
                }

                ExecutionPlaneLifecycleResult recovery;

                try
                {
                        recovery =
                                await _executionPlaneLifecycle
                                        .RecoverAsync(
                                                job.Id,
                                                job.ExternalId,
                                                cancellationToken);
                }
                catch (OperationCanceledException)
                        when (cancellationToken
                                .IsCancellationRequested)
                {
                        throw;
                }
                catch
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.RetryableFailure,
                                JobApplicationErrors
                                        .ExecutionPlanePreparationFailed);
                }

                if (!recovery.IsSuccess ||
                    recovery.Session is null)
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.RetryableFailure,
                                JobApplicationErrors
                                        .ExecutionPlanePreparationFailed);
                }

                if (recovery.Session.Repository.JobId != job.Id ||
                    !string.Equals(
                            recovery.Session.Repository.JobExternalId,
                            job.ExternalId,
                            StringComparison.Ordinal) ||
                    !string.Equals(
                            recovery.Session.Repository.Head,
                            job.BaseRepositoryHead,
                            StringComparison.OrdinalIgnoreCase))
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors
                                        .ExecutionPlaneRepositoryMismatch);
                }

                return await TransitionAndSaveAsync(
                        job,
                        JobState.Developing,
                        "Execution workspace recovered and verified against the pinned revision.",
                        actor,
                        correlationId,
                        cancellationToken);
        }

        private async Task<JobOperationResult>
                AdvanceDevelopingAsync(
                        Job job,
                        string actor,
                        string correlationId,
                        CancellationToken cancellationToken)
        {
                if (string.IsNullOrWhiteSpace(
                        job.BaseRepositoryHead))
                {
                        return JobOperationResult.Failure(
                                JobOperationKind
                                        .OperationalFailure,
                                JobApplicationErrors
                                        .MissingBaseRepositoryHead);
                }

                ExecutionPlaneLifecycleResult recovery;

                try
                {
                        recovery =
                                await _executionPlaneLifecycle
                                        .RecoverAsync(
                                                job.Id,
                                                job.ExternalId,
                                                cancellationToken);
                }
                catch (OperationCanceledException)
                        when (cancellationToken
                                .IsCancellationRequested)
                {
                        throw;
                }
                catch
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.RetryableFailure,
                                JobApplicationErrors
                                        .ExecutionPlanePreparationFailed);
                }

                if (!recovery.IsSuccess ||
                    recovery.Session is null)
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.RetryableFailure,
                                JobApplicationErrors
                                        .ExecutionPlanePreparationFailed);
                }

                if (recovery.Session.Repository.JobId != job.Id ||
                    !string.Equals(
                            recovery.Session.Repository.JobExternalId,
                            job.ExternalId,
                            StringComparison.Ordinal) ||
                    !string.Equals(
                            recovery.Session.Repository.Head,
                            job.BaseRepositoryHead,
                            StringComparison.OrdinalIgnoreCase))
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors
                                        .ExecutionPlaneRepositoryMismatch);
                }

                Guid runId =
                        _jobRunIdProvider.Create(
                                job.Id,
                                job.AttemptCount);

                StageRecoveryResult humanReviewCorrection =
                        await CheckRecoveryEvidenceAsync(
                                job, runId, RecoveryStage.HumanReviewCorrection,
                                correlationId, cancellationToken);
                JobOperationResult? humanRecoveryFailure =
                        RecoveryFailure(humanReviewCorrection);
                if (humanRecoveryFailure is not null)
                        return humanRecoveryFailure;
                bool expectsHumanReviewCorrection =
                        HasActiveHumanReviewCorrection(
                                job.State,
                                job.Transitions);
                if (expectsHumanReviewCorrection != humanReviewCorrection.IsCompleted)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.StageRecoveryFailed);
                if (expectsHumanReviewCorrection &&
                    humanReviewCorrection.HumanReviewCorrection is not null)
                        return await AdvanceHumanReviewDevelopingAsync(
                                job,
                                recovery.Session.Repository,
                                runId,
                                humanReviewCorrection.HumanReviewCorrection,
                                actor,
                                correlationId,
                                cancellationToken);

                ReviewerReview? correctionFeedback = null;
                Guid evidenceRunId = runId;
                if (job.AttemptCount > 1)
                {
                        evidenceRunId = _jobRunIdProvider.Create(job.Id, 1);
                        Guid previousRunId = _jobRunIdProvider.Create(job.Id, job.AttemptCount - 1);
                        StageRecoveryResult previousReview = await CheckRecoveryEvidenceAsync(
                                job, previousRunId, RecoveryStage.Reviewer, correlationId, cancellationToken);
                        JobOperationResult? previousFailure = RecoveryFailure(previousReview);
                        if (previousFailure is not null) return previousFailure;
                        if (!previousReview.IsCompleted || previousReview.ReviewerReview is null ||
                            previousReview.ReviewerReview.Decision != ReviewerDecision.ChangesRequired)
                                return JobOperationResult.Failure(JobOperationKind.PermanentFailure, JobApplicationErrors.ReviewerEvidenceInvalid);
                        correctionFeedback = previousReview.ReviewerReview;
                }

                StageRecoveryResult recoveredDeveloper =
                        await CheckRecoveryEvidenceAsync(
                                job, runId, RecoveryStage.Developer,
                                correlationId, cancellationToken);

                ValidatedDeveloperProposal proposal;

                if (recoveredDeveloper.IsCompleted &&
                    recoveredDeveloper.DeveloperProposal is not null)
                {
                        proposal = recoveredDeveloper.DeveloperProposal;
                }
                else
                {
                        JobOperationResult? developerRecoveryFailure =
                                RecoveryFailure(recoveredDeveloper);
                        if (developerRecoveryFailure is not null)
                                return developerRecoveryFailure;

                DeveloperExecutionResult developer;

                try
                {
                        developer =
                                await _developerExecutionService
                                        .ExecuteAsync(
                                                new DeveloperExecutionRequest
                                                {
                                                        JobId = job.Id,
                                                        RunId = runId,
                                                        JobRequest = job.Request,
                                                        CorrelationId = correlationId,
                                                        Repository = recovery.Session.Repository,
                                                        AuthorizedEvidenceRunId = evidenceRunId,
                                                        ReviewerFeedback = correctionFeedback
                                                },
                                                cancellationToken);
                }
                catch (OperationCanceledException)
                        when (cancellationToken.IsCancellationRequested)
                {
                        throw;
                }
                catch
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.RetryableFailure,
                                JobApplicationErrors.DeveloperExecutionFailed);
                }

                if (!developer.IsSuccess ||
                    developer.Proposal is null)
                {
                        return JobOperationResult.Failure(
                                MapDeveloperFailureKind(
                                        developer.FailureKind),
                                JobApplicationErrors.DeveloperExecutionFailed);
                }

                        proposal = developer.Proposal;
                }

                SafeChangeApplicationResult safeChange;

                try
                {
                        safeChange =
                                await _safeChangeApplier.ApplyAsync(
                                        new SafeChangeApplicationRequest
                                        {
                                                JobId = job.Id,
                                                RunId = runId,
                                                AttemptCount = job.AttemptCount,
                                                Repository = recovery.Session.Repository,
                                                Proposal = proposal,
                                                CorrelationId = correlationId,
                                                ProposalLineageId =
                                                        $"developer:{correlationId}"
                                        },
                                        cancellationToken);
                }
                catch (OperationCanceledException)
                        when (cancellationToken.IsCancellationRequested)
                {
                        throw;
                }
                catch
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.RetryableFailure,
                                JobApplicationErrors.SafeChangeApplicationFailed);
                }

                if (!safeChange.IsSuccess)
                {
                        return JobOperationResult.Failure(
                                MapSafeChangeFailureKind(
                                        safeChange.FailureKind),
                                JobApplicationErrors.SafeChangeApplicationFailed);
                }

                StageRecoveryResult recoveredObserved = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.ObservedChanges, correlationId, cancellationToken);
                JobOperationResult? observedRecoveryFailure = RecoveryFailure(recoveredObserved);
                if (observedRecoveryFailure is not null) return observedRecoveryFailure;
                if (!recoveredObserved.IsCompleted)
                {
                        ObservedChangeEvidenceResult observedResult = await _observedChangeEvidenceService.CaptureAsync(
                                new ObservedChangeEvidenceRequest
                                {
                                        JobId=job.Id, RunId=runId, Repository=recovery.Session.Repository,
                                        Proposal=proposal, CorrelationId=correlationId
                                }, cancellationToken);
                        if (!observedResult.IsSuccess)
                                return JobOperationResult.Failure(
                                        JobOperationKind.PermanentFailure,
                                        JobApplicationErrors.ObservedChangeEvidenceInvalid);
                }

                StageRecoveryResult recoveredRestore =
                        await CheckRecoveryEvidenceAsync(
                                job,
                                runId,
                                RecoveryStage.Restore,
                                correlationId,
                                cancellationToken);

                if (recoveredRestore.IsCompleted)
                {
                        return await TransitionAndSaveAsync(
                                job,
                                JobState.Building,
                                "Restore evidence recovered; restore was not repeated.",
                                actor,
                                correlationId,
                                cancellationToken);
                }

                JobOperationResult? restoreRecoveryFailure =
                        RecoveryFailure(
                                recoveredRestore);

                if (restoreRecoveryFailure is not null)
                {
                        return restoreRecoveryFailure;
                }

                RestoreExecutionResult restore;

                try
                {
                        restore =
                                await _restoreExecutionService
                                        .ExecuteAsync(
                                                new RestoreExecutionRequest
                                                {
                                                        JobId =
                                                                job.Id,

                                                        RunId =
                                                                runId,

                                                        Repository =
                                                                recovery
                                                                        .Session
                                                                        .Repository,

                                                        Target =
                                                                _executionTargetProvider
                                                                        .DotnetTarget,

                                                        CorrelationId =
                                                                correlationId
                                                },
                                                cancellationToken);
                }
                catch (OperationCanceledException)
                        when (cancellationToken
                                .IsCancellationRequested)
                {
                        throw;
                }
                catch
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.RetryableFailure,
                                JobApplicationErrors
                                        .RestoreExecutionFailed);
                }

                if (!restore.IsSuccess)
                {
                        return JobOperationResult.Failure(
                                MapRestoreFailureKind(
                                        restore.FailureKind),
                                JobApplicationErrors
                                        .RestoreExecutionFailed);
                }

                return await TransitionAndSaveAsync(
                        job,
                        JobState.Building,
                        "Restore completed successfully and evidence persisted.",
                        actor,
                        correlationId,
                        cancellationToken);
        }

        private async Task<JobOperationResult> AdvanceHumanReviewDevelopingAsync(
                Job job,
                RepositoryWorktreeHandle repository,
                Guid runId,
                HumanReviewCorrectionEvidence humanEvidence,
                string actor,
                string correlationId,
                CancellationToken cancellationToken)
        {
                StageRecoveryResult currentDeveloper = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.Developer,
                        correlationId, cancellationToken);
                StageRecoveryResult build = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.Build,
                        correlationId, cancellationToken);
                StageRecoveryResult test = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.Test,
                        correlationId, cancellationToken);
                StageRecoveryResult reviewer = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.ReviewerOriginal,
                        correlationId, cancellationToken);

                foreach (StageRecoveryResult evidence in new[] { currentDeveloper, build, test, reviewer })
                {
                        JobOperationResult? failure = RecoveryFailure(evidence);
                        if (failure is not null) return failure;
                        if (!evidence.IsCompleted)
                                return JobOperationResult.Failure(
                                        JobOperationKind.PermanentFailure,
                                        JobApplicationErrors.StageRecoveryFailed);
                }

                StageRecoveryResult recoveredCorrection = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.DeveloperHumanReviewCorrection,
                        correlationId, cancellationToken);
                JobOperationResult? correctionFailure = RecoveryFailure(recoveredCorrection);
                if (correctionFailure is not null) return correctionFailure;

                ValidatedDeveloperProposal correctionProposal;
                if (recoveredCorrection.IsCompleted && recoveredCorrection.DeveloperProposal is not null)
                {
                        correctionProposal = recoveredCorrection.DeveloperProposal;
                }
                else
                {
                        DeveloperExecutionResult executed = await _developerExecutionService.ExecuteAsync(
                                new DeveloperExecutionRequest
                                {
                                        JobId = job.Id,
                                        RunId = runId,
                                        JobRequest = job.Request,
                                        CorrelationId = correlationId,
                                        Repository = repository,
                                        AuthorizedEvidenceRunId = runId,
                                        HumanReviewCorrection = new DeveloperHumanReviewCorrectionContext
                                        {
                                                CurrentProposal = currentDeveloper.DeveloperProposal!,
                                                BuildReport = build.BuildReport!,
                                                TestReport = test.TestReport!,
                                                ReviewerReview = reviewer.ReviewerReview!,
                                                HumanReviewEvidence = humanEvidence
                                        }
                                }, cancellationToken);
                        if (!executed.IsSuccess || executed.Proposal is null)
                                return JobOperationResult.Failure(
                                        MapDeveloperFailureKind(executed.FailureKind),
                                        JobApplicationErrors.DeveloperExecutionFailed);
                        correctionProposal = executed.Proposal;
                }

                SafeChangeApplicationResult safeChange = await _safeChangeApplier.ApplyAsync(
                        new SafeChangeApplicationRequest
                        {
                                JobId = job.Id,
                                RunId = runId,
                                AttemptCount = job.AttemptCount,
                                Repository = repository,
                                Proposal = correctionProposal,
                                CorrelationId = correlationId,
                                ProposalLineageId =
                                        $"human-review-correction:{correlationId}",
                                IsHumanReviewCorrection = true
                        }, cancellationToken);
                if (!safeChange.IsSuccess)
                        return JobOperationResult.Failure(
                                MapSafeChangeFailureKind(safeChange.FailureKind),
                                JobApplicationErrors.SafeChangeApplicationFailed);

                StageRecoveryResult observed = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.ObservedHumanReviewCorrection,
                        correlationId, cancellationToken);
                JobOperationResult? observedFailure = RecoveryFailure(observed);
                if (observedFailure is not null) return observedFailure;
                if (!observed.IsCompleted)
                {
                        StageRecoveryResult effective = await CheckRecoveryEvidenceAsync(
                                job, runId, RecoveryStage.Developer,
                                correlationId, cancellationToken);
                        if (!effective.IsCompleted || effective.DeveloperProposal is null)
                                return JobOperationResult.Failure(
                                        JobOperationKind.PermanentFailure,
                                        JobApplicationErrors.StageRecoveryFailed);
                        ObservedChangeEvidenceResult captured = await _observedChangeEvidenceService.CaptureAsync(
                                new ObservedChangeEvidenceRequest
                                {
                                        JobId = job.Id,
                                        RunId = runId,
                                        Repository = repository,
                                        Proposal = effective.DeveloperProposal,
                                        CorrelationId = correlationId,
                                        IsHumanReviewCorrection = true
                                }, cancellationToken);
                        if (!captured.IsSuccess)
                                return JobOperationResult.Failure(
                                        JobOperationKind.PermanentFailure,
                                        JobApplicationErrors.ObservedChangeEvidenceInvalid);
                }

                return await TransitionAndSaveAsync(
                        job, JobState.Building,
                        "Human review correction applied with governed evidence.",
                        actor, correlationId, cancellationToken);
        }

        private async Task<JobOperationResult>
                AdvanceBuildingAsync(
                        Job job,
                        string actor,
                        string correlationId,
                        CancellationToken cancellationToken)
        {
                if (string.IsNullOrWhiteSpace(
                        job.BaseRepositoryHead))
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.OperationalFailure,
                                JobApplicationErrors
                                        .MissingBaseRepositoryHead);
                }

                ExecutionPlaneLifecycleResult recovery;

                try
                {
                        recovery =
                                await _executionPlaneLifecycle
                                        .RecoverAsync(
                                                job.Id,
                                                job.ExternalId,
                                                cancellationToken);
                }
                catch (OperationCanceledException)
                        when (cancellationToken
                                .IsCancellationRequested)
                {
                        throw;
                }
                catch
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.RetryableFailure,
                                JobApplicationErrors
                                        .ExecutionPlanePreparationFailed);
                }

                if (!recovery.IsSuccess ||
                    recovery.Session is null)
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.RetryableFailure,
                                JobApplicationErrors
                                        .ExecutionPlanePreparationFailed);
                }

                if (recovery.Session.Repository.JobId != job.Id ||
                    !string.Equals(
                            recovery.Session.Repository.JobExternalId,
                            job.ExternalId,
                            StringComparison.Ordinal) ||
                    !string.Equals(
                            recovery.Session.Repository.Head,
                            job.BaseRepositoryHead,
                            StringComparison.OrdinalIgnoreCase))
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors
                                        .ExecutionPlaneRepositoryMismatch);
                }

                Guid runId =
                        _jobRunIdProvider.Create(
                                job.Id,
                                job.AttemptCount);

                EffectiveGovernedCorrectionResolution governedResolution =
                        await ResolveEffectiveGovernedCorrectionAsync(
                                job,
                                runId,
                                cancellationToken);
                if (!governedResolution.IsValid)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.StageRecoveryFailed);
                bool isGovernedHumanCorrection =
                        governedResolution.CorrelationId is not null;

                StageRecoveryResult humanCorrection = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.HumanReviewCorrection,
                        correlationId, cancellationToken);
                JobOperationResult? humanCorrectionFailure = RecoveryFailure(humanCorrection);
                if (humanCorrectionFailure is not null) return humanCorrectionFailure;
                bool isHumanReviewCorrection = HasActiveHumanReviewCorrection(
                        job.State,
                        job.Transitions);
                if (isHumanReviewCorrection != humanCorrection.IsCompleted)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.StageRecoveryFailed);

                StageRecoveryResult recoveredBuild =
                        await CheckRecoveryEvidenceAsync(
                                job,
                                runId,
                                isHumanReviewCorrection
                                        ? RecoveryStage.BuildHumanReviewCorrection
                                        : isGovernedHumanCorrection
                                        ? RecoveryStage.BuildGovernedHumanCorrection
                                        : RecoveryStage.Build,
                                correlationId,
                                cancellationToken);

                if (recoveredBuild.IsCompleted)
                {
                        return await TransitionAndSaveAsync(
                                job,
                                JobState.Testing,
                                "Build evidence recovered; build was not repeated.",
                                actor,
                                correlationId,
                                cancellationToken);
                }

                if (recoveredBuild.Status ==
                        StageRecoveryStatus.FailedExecution &&
                    recoveredBuild.BuildReport is not null &&
                    !string.IsNullOrWhiteSpace(
                        recoveredBuild.BuildStandardOutput))
                {
                        StageRecoveryResult originalDeveloper =
                                await CheckRecoveryEvidenceAsync(
                                        job,
                                        runId,
                                        RecoveryStage.DeveloperOriginal,
                                        correlationId,
                                        cancellationToken);

                        JobOperationResult? originalDeveloperFailure =
                                RecoveryFailure(originalDeveloper);
                        if (originalDeveloperFailure is not null)
                        {
                                return originalDeveloperFailure;
                        }

                        if (!originalDeveloper.IsCompleted ||
                            originalDeveloper.DeveloperProposal is null)
                        {
                                return JobOperationResult.Failure(
                                        JobOperationKind.PermanentFailure,
                                        JobApplicationErrors
                                                .StageRecoveryFailed);
                        }

                        StageRecoveryResult recoveredCorrection =
                                await CheckRecoveryEvidenceAsync(
                                        job,
                                        runId,
                                        RecoveryStage
                                                .DeveloperBuildCorrection,
                                        correlationId,
                                        cancellationToken);

                        StageRecoveryResult recoveredRetry =
                                await CheckRecoveryEvidenceAsync(
                                        job,
                                        runId,
                                        RecoveryStage.DeveloperBuildCorrectionRetry,
                                        correlationId,
                                        cancellationToken);
                        JobOperationResult? retryRecoveryFailure =
                                RecoveryFailure(recoveredRetry);
                        if (retryRecoveryFailure is not null)
                                return retryRecoveryFailure;

                        bool isBuildCorrectionRetry = recoveredRetry.IsCompleted;
                        if (isBuildCorrectionRetry)
                                recoveredCorrection = recoveredRetry;
                        bool previousNoOp =
                                !isBuildCorrectionRetry &&
                                recoveredCorrection.IsCompleted &&
                                recoveredCorrection.DeveloperProposal is not null &&
                                BuildCorrectionNoOpPolicy.IsEntireNoOp(
                                        recoveredCorrection.DeveloperProposal);

                        ValidatedDeveloperProposal correctionProposal;

                        if (recoveredCorrection.IsCompleted &&
                            recoveredCorrection.DeveloperProposal is not null &&
                            !previousNoOp)
                        {
                                correctionProposal =
                                        recoveredCorrection
                                                .DeveloperProposal;
                        }
                        else
                        {
                                JobOperationResult? correctionRecoveryFailure =
                                        RecoveryFailure(recoveredCorrection);
                                if (correctionRecoveryFailure is not null)
                                {
                                        return correctionRecoveryFailure;
                                }

                                IReadOnlyList<string> noOpPaths = [];
                                if (previousNoOp)
                                {
                                        StageRecoveryResult previousObserved =
                                                await CheckRecoveryEvidenceAsync(
                                                        job, runId,
                                                        RecoveryStage.ObservedBuildCorrection,
                                                        correlationId,
                                                        cancellationToken);
                                        JobOperationResult? observedFailure =
                                                RecoveryFailure(previousObserved);
                                        if (observedFailure is not null)
                                                return observedFailure;
                                        if (!previousObserved.IsCompleted ||
                                            previousObserved.ObservedChangeManifest is null)
                                                return JobOperationResult.Failure(
                                                        JobOperationKind.PermanentFailure,
                                                        JobApplicationErrors.StageRecoveryFailed);
                                        noOpPaths = BuildCorrectionNoOpPolicy.NoOpPaths(
                                                recoveredCorrection.DeveloperProposal!);
                                        if (noOpPaths.Any(path =>
                                        {
                                                ValidatedDeveloperChange change =
                                                        recoveredCorrection.DeveloperProposal!.Changes
                                                                .Single(item => string.Equals(
                                                                        item.RelativePath, path,
                                                                        StringComparison.OrdinalIgnoreCase));
                                                return !previousObserved.ObservedChangeManifest.Entries.Any(
                                                        entry => string.Equals(entry.RelativePath, path,
                                                                    StringComparison.OrdinalIgnoreCase) &&
                                                                 string.Equals(entry.FinalSha256,
                                                                    change.ExpectedContentSha256,
                                                                    StringComparison.OrdinalIgnoreCase));
                                        }))
                                                return JobOperationResult.Failure(
                                                        JobOperationKind.PermanentFailure,
                                                        JobApplicationErrors.StageRecoveryFailed);
                                        isBuildCorrectionRetry = true;
                                }

                                DeveloperExecutionResult correction;

                                try
                                {
                                        correction =
                                                await _developerExecutionService
                                                        .ExecuteAsync(
                                                                new DeveloperExecutionRequest
                                                                {
                                                                        JobId = job.Id,
                                                                        RunId = runId,
                                                                        JobRequest = job.Request,
                                                                        CorrelationId = correlationId,
                                                                        Repository = recovery.Session.Repository,
                                                                        AuthorizedEvidenceRunId = runId,
                                                                        BuildCorrection = new DeveloperBuildCorrectionContext
                                                                        {
                                                                                OriginalProposal = originalDeveloper.DeveloperProposal,
                                                                                FailedBuildReport = recoveredBuild.BuildReport,
                                                                                BuildStandardOutput = recoveredBuild.BuildStandardOutput,
                                                                                PreviousNoOpPaths = noOpPaths
                                                                        }
                                                                },
                                                                cancellationToken);
                                }
                                catch (OperationCanceledException)
                                        when (cancellationToken
                                                .IsCancellationRequested)
                                {
                                        throw;
                                }
                                catch
                                {
                                        return JobOperationResult.Failure(
                                                JobOperationKind.RetryableFailure,
                                                JobApplicationErrors
                                                        .DeveloperExecutionFailed);
                                }

                                if (!correction.IsSuccess ||
                                    correction.Proposal is null)
                                {
                                        return JobOperationResult.Failure(
                                                MapDeveloperFailureKind(
                                                        correction.FailureKind),
                                                JobApplicationErrors
                                                        .DeveloperExecutionFailed);
                                }

                                correctionProposal = correction.Proposal;
                        }

                        if (BuildCorrectionNoOpPolicy.IsEntireNoOp(correctionProposal))
                                return JobOperationResult.Failure(
                                        JobOperationKind.PermanentFailure,
                                        JobApplicationErrors.DeveloperExecutionFailed);

                        SafeChangeApplicationResult correctedSafeChange;

                        try
                        {
                                correctedSafeChange =
                                        await _safeChangeApplier.ApplyAsync(
                                                new SafeChangeApplicationRequest
                                                {
                                                        JobId = job.Id,
                                                        RunId = runId,
                                                        AttemptCount = job.AttemptCount,
                                                        Repository = recovery.Session.Repository,
                                                        Proposal = correctionProposal,
                                                        CorrelationId = correlationId,
                                                        ProposalLineageId =
                                                                (isBuildCorrectionRetry
                                                                        ? "build-correction-retry:"
                                                                        : "build-correction:") +
                                                                correlationId,
                                                        IsBuildCorrection = true,
                                                        IsBuildCorrectionRetry = isBuildCorrectionRetry
                                                },
                                                cancellationToken);
                        }
                        catch (OperationCanceledException)
                                when (cancellationToken.IsCancellationRequested)
                        {
                                throw;
                        }
                        catch
                        {
                                return JobOperationResult.Failure(
                                        JobOperationKind.RetryableFailure,
                                        JobApplicationErrors
                                                .SafeChangeApplicationFailed);
                        }

                        if (!correctedSafeChange.IsSuccess)
                        {
                                return JobOperationResult.Failure(
                                        MapSafeChangeFailureKind(
                                                correctedSafeChange.FailureKind),
                                        JobApplicationErrors
                                                .SafeChangeApplicationFailed);
                        }

                        StageRecoveryResult recoveredCorrectedObserved =
                                await CheckRecoveryEvidenceAsync(
                                        job,
                                        runId,
                                        isBuildCorrectionRetry
                                                ? RecoveryStage.ObservedBuildCorrectionRetry
                                                : RecoveryStage.ObservedBuildCorrection,
                                        correlationId,
                                        cancellationToken);

                        JobOperationResult? correctedObservedFailure =
                                RecoveryFailure(recoveredCorrectedObserved);
                        if (correctedObservedFailure is not null)
                        {
                                return correctedObservedFailure;
                        }

                        if (!recoveredCorrectedObserved.IsCompleted)
                        {
                                StageRecoveryResult effectiveDeveloper =
                                        await CheckRecoveryEvidenceAsync(
                                                job,
                                                runId,
                                                RecoveryStage.Developer,
                                                correlationId,
                                                cancellationToken);

                                JobOperationResult? effectiveDeveloperFailure =
                                        RecoveryFailure(effectiveDeveloper);
                                if (effectiveDeveloperFailure is not null)
                                {
                                        return effectiveDeveloperFailure;
                                }

                                if (!effectiveDeveloper.IsCompleted ||
                                    effectiveDeveloper.DeveloperProposal is null)
                                {
                                        return JobOperationResult.Failure(
                                                JobOperationKind.PermanentFailure,
                                                JobApplicationErrors
                                                        .StageRecoveryFailed);
                                }

                                ObservedChangeEvidenceResult correctedObserved =
                                        await _observedChangeEvidenceService
                                                .CaptureAsync(
                                                        new ObservedChangeEvidenceRequest
                                                        {
                                                                JobId = job.Id,
                                                                RunId = runId,
                                                                Repository = recovery.Session.Repository,
                                                                Proposal = effectiveDeveloper.DeveloperProposal,
                                                                CorrelationId = correlationId,
                                                                IsBuildCorrection = true,
                                                                IsBuildCorrectionRetry = isBuildCorrectionRetry
                                                        },
                                                        cancellationToken);

                                if (!correctedObserved.IsSuccess)
                                {
                                        return JobOperationResult.Failure(
                                                JobOperationKind.PermanentFailure,
                                                JobApplicationErrors
                                                        .ObservedChangeEvidenceInvalid);
                                }
                        }

                        BuildExecutionResult correctedBuild;

                        try
                        {
                                correctedBuild =
                                        await _buildExecutionService.ExecuteAsync(
                                                new BuildExecutionRequest
                                                {
                                                        JobId = job.Id,
                                                        RunId = runId,
                                                        Repository = recovery.Session.Repository,
                                                        Target = _executionTargetProvider.DotnetTarget,
                                                        CorrelationId = correlationId,
                                                        IsBuildCorrection = true,
                                                        IsBuildCorrectionRetry = isBuildCorrectionRetry
                                                },
                                                cancellationToken);
                        }
                        catch (OperationCanceledException)
                                when (cancellationToken.IsCancellationRequested)
                        {
                                throw;
                        }
                        catch
                        {
                                return JobOperationResult.Failure(
                                        JobOperationKind.RetryableFailure,
                                        JobApplicationErrors
                                                .BuildExecutionFailed);
                        }

                        if (!correctedBuild.IsSuccess)
                        {
                                return JobOperationResult.Failure(
                                        MapBuildFailureKind(
                                                correctedBuild.FailureKind),
                                        JobApplicationErrors
                                                .BuildExecutionFailed);
                        }

                        return await TransitionAndSaveAsync(
                                job,
                                JobState.Testing,
                                "Build correction completed successfully and evidence persisted.",
                                actor,
                                correlationId,
                                cancellationToken);
                }

                JobOperationResult? buildRecoveryFailure =
                        RecoveryFailure(
                                recoveredBuild);

                if (buildRecoveryFailure is not null)
                {
                        return buildRecoveryFailure;
                }

                BuildExecutionResult build;

                try
                {
                        build =
                                await _buildExecutionService
                                        .ExecuteAsync(
                                                new BuildExecutionRequest
                                                {
                                                        JobId =
                                                                job.Id,

                                                        RunId =
                                                                runId,

                                                        Repository =
                                                                recovery
                                                                        .Session
                                                                        .Repository,

                                                        Target =
                                                                _executionTargetProvider
                                                                        .DotnetTarget,

                                                        CorrelationId =
                                                                correlationId,

                                                        IsHumanReviewCorrection =
                                                                isHumanReviewCorrection
                                                },
                                                cancellationToken);
                }
                catch (OperationCanceledException)
                        when (cancellationToken
                                .IsCancellationRequested)
                {
                        throw;
                }
                catch
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.RetryableFailure,
                                JobApplicationErrors
                                        .BuildExecutionFailed);
                }

                if (!build.IsSuccess)
                {
                        return JobOperationResult.Failure(
                                MapBuildFailureKind(
                                        build.FailureKind),
                                JobApplicationErrors
                                        .BuildExecutionFailed);
                }

                return await TransitionAndSaveAsync(
                        job,
                        JobState.Testing,
                        "Build completed successfully and evidence persisted.",
                        actor,
                        correlationId,
                        cancellationToken);
        }

        private async Task<JobOperationResult>
                AdvanceTestingAsync(
                        Job job,
                        string actor,
                        string correlationId,
                        CancellationToken cancellationToken)
        {
                if (string.IsNullOrWhiteSpace(
                        job.BaseRepositoryHead))
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.OperationalFailure,
                                JobApplicationErrors
                                        .MissingBaseRepositoryHead);
                }

                ExecutionPlaneLifecycleResult recovery;

                try
                {
                        recovery =
                                await _executionPlaneLifecycle
                                        .RecoverAsync(
                                                job.Id,
                                                job.ExternalId,
                                                cancellationToken);
                }
                catch (OperationCanceledException)
                        when (cancellationToken
                                .IsCancellationRequested)
                {
                        throw;
                }
                catch
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.RetryableFailure,
                                JobApplicationErrors
                                        .ExecutionPlanePreparationFailed);
                }

                if (!recovery.IsSuccess ||
                    recovery.Session is null)
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.RetryableFailure,
                                JobApplicationErrors
                                        .ExecutionPlanePreparationFailed);
                }

                if (recovery.Session.Repository.JobId != job.Id ||
                    !string.Equals(
                            recovery.Session.Repository.JobExternalId,
                            job.ExternalId,
                            StringComparison.Ordinal) ||
                    !string.Equals(
                            recovery.Session.Repository.Head,
                            job.BaseRepositoryHead,
                            StringComparison.OrdinalIgnoreCase))
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors
                                        .ExecutionPlaneRepositoryMismatch);
                }

                Guid runId =
                        _jobRunIdProvider.Create(
                                job.Id,
                                job.AttemptCount);

                StageRecoveryResult humanCorrection = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.HumanReviewCorrection,
                        correlationId, cancellationToken);
                JobOperationResult? humanCorrectionFailure = RecoveryFailure(humanCorrection);
                if (humanCorrectionFailure is not null) return humanCorrectionFailure;
                bool isHumanReviewCorrection = HasActiveHumanReviewCorrection(
                        job.State,
                        job.Transitions);
                if (isHumanReviewCorrection != humanCorrection.IsCompleted)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.StageRecoveryFailed);

                EffectiveGovernedCorrectionResolution governedResolution =
                        await ResolveEffectiveGovernedCorrectionAsync(
                                job,
                                runId,
                                cancellationToken);
                if (!governedResolution.IsValid)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.StageRecoveryFailed);
                bool isGovernedHumanCorrection =
                        governedResolution.CorrelationId is not null;

                StageRecoveryResult recoveredTest =
                        await CheckRecoveryEvidenceAsync(
                                job,
                                runId,
                                isHumanReviewCorrection
                                        ? RecoveryStage.TestHumanReviewCorrection
                                        : isGovernedHumanCorrection
                                        ? RecoveryStage.TestGovernedHumanCorrection
                                        : RecoveryStage.Test,
                                correlationId,
                                cancellationToken);

                if (recoveredTest.IsCompleted)
                {
                        return await TransitionAndSaveAsync(
                                job,
                                JobState.Reviewing,
                                "Test evidence recovered; tests were not repeated.",
                                actor,
                                correlationId,
                                cancellationToken);
                }

                JobOperationResult? testRecoveryFailure =
                        RecoveryFailure(
                                recoveredTest);

                if (testRecoveryFailure is not null)
                {
                        return testRecoveryFailure;
                }

                TestExecutionResult test;

                try
                {
                        test =
                                await _testExecutionService
                                        .ExecuteAsync(
                                                new TestExecutionRequest
                                                {
                                                        JobId =
                                                                job.Id,

                                                        RunId =
                                                                runId,

                                                        Repository =
                                                                recovery
                                                                        .Session
                                                                        .Repository,

                                                        Target =
                                                                _executionTargetProvider
                                                                        .DotnetTarget,

                                                        CorrelationId =
                                                                correlationId,

                                                        IsHumanReviewCorrection =
                                                                isHumanReviewCorrection ||
                                                                isGovernedHumanCorrection,
                                                        IsGovernedHumanCorrection =
                                                                isGovernedHumanCorrection
                                                },
                                                cancellationToken);
                }
                catch (OperationCanceledException)
                        when (cancellationToken
                                .IsCancellationRequested)
                {
                        throw;
                }
                catch
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.RetryableFailure,
                                JobApplicationErrors
                                        .TestExecutionFailed);
                }

                if (!test.IsSuccess)
                {
                        return JobOperationResult.Failure(
                                MapTestFailureKind(
                                        test.FailureKind),
                                JobApplicationErrors
                                        .TestExecutionFailed);
                }

                return await TransitionAndSaveAsync(
                        job,
                        JobState.Reviewing,
                        "Tests completed successfully and evidence persisted.",
                        actor,
                        correlationId,
                        cancellationToken);
        }

        private async Task<JobOperationResult> AdvanceReviewingAsync(
                Job job, string actor, string correlationId, CancellationToken cancellationToken)
        {
                Guid runId = _jobRunIdProvider.Create(job.Id, job.AttemptCount);
                EffectiveGovernedCorrectionResolution governedResolution =
                        await ResolveEffectiveGovernedCorrectionAsync(
                                job,
                                runId,
                                cancellationToken);
                if (!governedResolution.IsValid)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.StageRecoveryFailed);
                bool isGovernedHumanCorrection =
                        governedResolution.CorrelationId is not null;
                string evidenceCorrelationId =
                        governedResolution.CorrelationId ?? correlationId;
                StageRecoveryResult humanCorrection = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.HumanReviewCorrection,
                        correlationId, cancellationToken);
                JobOperationResult? humanCorrectionFailure = RecoveryFailure(humanCorrection);
                if (humanCorrectionFailure is not null) return humanCorrectionFailure;
                bool isHumanReviewCorrection = HasActiveHumanReviewCorrection(
                        job.State,
                        job.Transitions);
                if (isHumanReviewCorrection != humanCorrection.IsCompleted)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.StageRecoveryFailed);
                Guid planningRunId = _jobRunIdProvider.Create(job.Id, 1);
                StageRecoveryResult plan = await CheckRecoveryEvidenceAsync(job, planningRunId, RecoveryStage.Planning, correlationId, cancellationToken);
                StageRecoveryResult developer = await CheckRecoveryEvidenceAsync(
                        job, runId, RecoveryStage.EffectiveDeveloperProposal,
                        evidenceCorrelationId, cancellationToken);
                DeveloperProposalLineage expectedLineage =
                        isHumanReviewCorrection
                                ? DeveloperProposalLineage.HumanReviewCorrection
                                : isGovernedHumanCorrection
                                ? DeveloperProposalLineage.GovernedHumanCorrection
                                : developer.DeveloperProposalLineage ??
                                  DeveloperProposalLineage.Original;
                EffectiveEvidenceSelection? selection =
                        ResolveEffectiveEvidenceSelection(expectedLineage);
                if (developer.DeveloperProposalLineage != expectedLineage ||
                    selection is null)
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.ReviewerEvidenceInvalid);
                StageRecoveryResult observed = await CheckRecoveryEvidenceAsync(
                        job, runId, selection.ObservedStage,
                        evidenceCorrelationId, cancellationToken);
                StageRecoveryResult build = await CheckRecoveryEvidenceAsync(
                        job, runId, selection.BuildStage,
                        evidenceCorrelationId, cancellationToken);
                StageRecoveryResult test = isGovernedHumanCorrection
                        ? await RecoverEffectiveGovernedTestAsync(
                                job,
                                runId,
                                evidenceCorrelationId,
                                cancellationToken)
                        : await CheckRecoveryEvidenceAsync(
                                job, runId, selection.TestStage,
                                correlationId, cancellationToken);
                foreach (StageRecoveryResult evidence in new[] { plan, developer, observed, build, test })
                {
                        JobOperationResult? failure = RecoveryFailure(evidence);
                        if (failure is not null) return failure;
                        if (!evidence.IsCompleted)
                                return JobOperationResult.Failure(JobOperationKind.PermanentFailure, JobApplicationErrors.ReviewerEvidenceInvalid);
                }

                StageRecoveryResult recoveredReview = await CheckRecoveryEvidenceAsync(
                        job, runId, selection.ReviewingReviewerStage,
                        correlationId, cancellationToken);
                JobOperationResult? reviewRecoveryFailure = RecoveryFailure(recoveredReview);
                if (reviewRecoveryFailure is not null) return reviewRecoveryFailure;
                ReviewerReview? review = recoveredReview.ReviewerReview;
                if (!recoveredReview.IsCompleted)
                {
                        ReviewerEffectiveSourceSnapshot? effectiveSource =
                                await CaptureReviewerEffectiveSourceAsync(
                                        job,
                                        runId,
                                        plan.PlannerPlan!,
                                        developer.DeveloperProposal!,
                                        observed.ObservedChangeManifest!,
                                        expectedLineage,
                                        cancellationToken);
                        if (effectiveSource is null)
                                return JobOperationResult.Failure(
                                        JobOperationKind.PermanentFailure,
                                        JobApplicationErrors.ReviewerEvidenceInvalid);

                        ReviewerExecutionResult executed = await _reviewerExecutionService.ExecuteAsync(new ReviewerExecutionRequest
                        {
                                JobId=job.Id, RunId=runId, JobRequest=job.Request, Plan=plan.PlannerPlan!,
                                DeveloperProposal=developer.DeveloperProposal!, ObservedChanges=observed.ObservedChangeManifest!,
                                BuildReport=build.BuildReport!, TestReport=test.TestReport!, CorrelationId=correlationId,
                                HumanReviewCorrection = isHumanReviewCorrection
                                        ? humanCorrection.HumanReviewCorrection
                                        : null,
                                EffectiveProposalLineage = expectedLineage,
                                EffectiveSourceSnapshot = effectiveSource
                        }, cancellationToken);
                        if (!executed.IsSuccess || executed.Review is null)
                                return JobOperationResult.Failure(JobOperationKind.PermanentFailure, JobApplicationErrors.ReviewerExecutionFailed);
                        review = executed.Review;
                }

                ReviewDecisionResult decision = _reviewDecisionPolicy.Evaluate(new ReviewDecisionInput(
                        job.Id, runId, review!, build.BuildReport!, test.TestReport!, observed.ObservedChangeManifest!));
                if (!decision.IsSuccess || !decision.Decision.HasValue)
                        return JobOperationResult.Failure(JobOperationKind.PermanentFailure, JobApplicationErrors.ReviewDecisionFailed);

                if (!isHumanReviewCorrection &&
                    !isGovernedHumanCorrection &&
                    decision.Decision.Value == ReviewerDecision.ChangesRequired &&
                    job.AttemptCount < job.Limits.MaxDevelopmentAttempts)
                {
                        Result correction = job.BeginDevelopmentCorrection(
                                _clock.UtcNow, "Reviewer requested governed corrections.", actor, correlationId);
                        if (correction.IsFailure)
                                return JobOperationResult.Failure(JobOperationKind.InvalidTransition, correction.Error);
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                        return JobOperationResult.Success();
                }

                return await TransitionAndSaveAsync(job, JobState.WaitingHuman,
                        $"KRONXY review decision: {decision.Decision.Value}.", actor, correlationId, cancellationToken);
        }

        private sealed record EffectiveEvidenceSelection(
                DeveloperProposalLineage Lineage,
                RecoveryStage ReviewingReviewerStage,
                RecoveryStage ApprovalReviewerStage,
                RecoveryStage ObservedStage,
                RecoveryStage BuildStage,
                RecoveryStage TestStage);

        private sealed record EffectiveGovernedCorrectionResolution(
                bool IsValid,
                string? CorrelationId)
        {
                public static EffectiveGovernedCorrectionResolution None() =>
                        new(true, null);

                public static EffectiveGovernedCorrectionResolution Valid(
                        string correlationId) =>
                        new(true, correlationId);

                public static EffectiveGovernedCorrectionResolution Invalid() =>
                        new(false, null);
        }

        private async Task<EffectiveGovernedCorrectionResolution>
                ResolveEffectiveGovernedCorrectionAsync(
                        Job job,
                        Guid runId,
                        CancellationToken cancellationToken)
        {
                if (_artifactMetadataRepository is null)
                        return EffectiveGovernedCorrectionResolution.None();

                IReadOnlyList<ArtifactRecord> artifacts =
                        await _artifactMetadataRepository.GetByJobAndRunAsync(
                                job.Id,
                                runId,
                                cancellationToken);
                ArtifactRecord? latestRequest = artifacts
                        .Where(artifact => artifact.ArtifactType ==
                                ArtifactType.GovernedHumanCorrectionRequest)
                        .OrderByDescending(artifact => artifact.CreatedAtUtc)
                        .ThenByDescending(artifact => artifact.ArtifactId)
                        .FirstOrDefault();
                if (latestRequest is null)
                        return EffectiveGovernedCorrectionResolution.None();

                string sourceCorrelationId = latestRequest.CorrelationId;
                StageRecoveryResult correction =
                        await CheckRecoveryEvidenceAsync(
                                job,
                                runId,
                                RecoveryStage.GovernedHumanCorrection,
                                sourceCorrelationId,
                                cancellationToken);
                StageRecoveryResult observed =
                        await CheckRecoveryEvidenceAsync(
                                job,
                                runId,
                                RecoveryStage.ObservedGovernedHumanCorrection,
                                sourceCorrelationId,
                                cancellationToken);
                StageRecoveryResult build =
                        await CheckRecoveryEvidenceAsync(
                                job,
                                runId,
                                RecoveryStage.BuildGovernedHumanCorrection,
                                sourceCorrelationId,
                                cancellationToken);

                return correction.IsCompleted &&
                       observed.IsCompleted &&
                       build.IsCompleted
                        ? EffectiveGovernedCorrectionResolution.Valid(
                                sourceCorrelationId)
                        : EffectiveGovernedCorrectionResolution.Invalid();
        }

        private async Task<StageRecoveryResult>
                RecoverEffectiveGovernedTestAsync(
                        Job job,
                        Guid runId,
                        string sourceCorrelationId,
                        CancellationToken cancellationToken)
        {
                string? testCorrelationId = job.Transitions
                        .Where(transition =>
                                transition.FromState == JobState.Testing &&
                                transition.ToState == JobState.Reviewing)
                        .OrderByDescending(transition => transition.OccurredOnUtc)
                        .Select(transition => transition.CorrelationId)
                        .FirstOrDefault();
                if (string.IsNullOrWhiteSpace(testCorrelationId))
                        return StageRecoveryResult.NotCompleted();

                StageRecoveryResult governedTest =
                        await CheckRecoveryEvidenceAsync(
                                job,
                                runId,
                                RecoveryStage.TestGovernedHumanCorrection,
                                testCorrelationId,
                                cancellationToken);
                if (governedTest.IsCompleted &&
                    await IsTestAfterEffectiveGovernedBuildAsync(
                            job.Id,
                            runId,
                            sourceCorrelationId,
                            testCorrelationId,
                            ArtifactType.TestGovernedHumanCorrectionReport,
                            cancellationToken))
                        return governedTest;
                if (governedTest.Status != StageRecoveryStatus.NotCompleted)
                        return governedTest;

                StageRecoveryResult legacyTest =
                        await CheckRecoveryEvidenceAsync(
                                job,
                                runId,
                                RecoveryStage.Test,
                                testCorrelationId,
                                cancellationToken);
                return legacyTest.IsCompleted &&
                       await IsTestAfterEffectiveGovernedBuildAsync(
                               job.Id,
                               runId,
                               sourceCorrelationId,
                               testCorrelationId,
                               ArtifactType.TestReport,
                               cancellationToken)
                        ? legacyTest
                        : StageRecoveryResult.InvalidEvidence(
                                "STAGE_RECOVERY_EFFECTIVE_LINEAGE_MISMATCH");
        }

        private async Task<bool> IsTestAfterEffectiveGovernedBuildAsync(
                Guid jobId,
                Guid runId,
                string sourceCorrelationId,
                string testCorrelationId,
                ArtifactType testReportType,
                CancellationToken cancellationToken)
        {
                if (_artifactMetadataRepository is null)
                        return false;

                IReadOnlyList<ArtifactRecord> artifacts =
                        await _artifactMetadataRepository.GetByJobAndRunAsync(
                                jobId,
                                runId,
                                cancellationToken);
                return GovernedHumanCorrectionPolicy
                        .IsTestBoundToLatestCorrection(
                                artifacts,
                                sourceCorrelationId,
                                testCorrelationId,
                                testReportType);
        }

        private static EffectiveEvidenceSelection?
                ResolveEffectiveEvidenceSelection(
                        DeveloperProposalLineage? lineage) =>
                lineage switch
                {
                        DeveloperProposalLineage.Original => new(
                                DeveloperProposalLineage.Original,
                                RecoveryStage.ReviewerOriginal,
                                RecoveryStage.ReviewerOriginal,
                                RecoveryStage.ObservedChanges,
                                RecoveryStage.Build,
                                RecoveryStage.Test),

                        DeveloperProposalLineage.BuildCorrection => new(
                                DeveloperProposalLineage.BuildCorrection,
                                RecoveryStage.ReviewerOriginal,
                                RecoveryStage.ReviewerOriginal,
                                RecoveryStage.ObservedChanges,
                                RecoveryStage.Build,
                                RecoveryStage.Test),

                        DeveloperProposalLineage.HumanReviewCorrection => new(
                                DeveloperProposalLineage.HumanReviewCorrection,
                                RecoveryStage.ReviewerHumanReviewCorrection,
                                RecoveryStage.ReviewerHumanReviewCorrectionSourceAwareSuperseding,
                                RecoveryStage.ObservedHumanReviewCorrection,
                                RecoveryStage.BuildHumanReviewCorrection,
                                RecoveryStage.TestHumanReviewCorrection),

                        DeveloperProposalLineage.GovernedHumanCorrection => new(
                                DeveloperProposalLineage.GovernedHumanCorrection,
                                RecoveryStage.ReviewerOriginal,
                                RecoveryStage.ReviewerOriginal,
                                RecoveryStage.ObservedGovernedHumanCorrection,
                                RecoveryStage.BuildGovernedHumanCorrection,
                                RecoveryStage.TestGovernedHumanCorrection),

                        _ => null
                };

        private static bool ValidApprovalLineage(
                Job job,
                StageRecoveryResult developer,
                ObservedChangeManifest observed,
                ReviewerEffectiveSourceSnapshot effectiveSource,
                EffectiveEvidenceSelection selection)
        {
                if (developer.DeveloperProposal is null ||
                    developer.DeveloperProposalLineage != selection.Lineage ||
                    observed.JobId != job.Id ||
                    observed.RunId != effectiveSource.RunId ||
                    effectiveSource.JobId != job.Id ||
                    effectiveSource.EffectiveProposalLineage != selection.Lineage ||
                    !ProposalMatchesObserved(
                            developer.DeveloperProposal,
                            observed))
                        return false;

                if (selection.Lineage !=
                    DeveloperProposalLineage.GovernedHumanCorrection)
                        return true;

                GovernedHumanCorrectionEvidence? correction =
                        developer.GovernedHumanCorrection;
                GovernedHumanCorrectionReceipt? receipt =
                        developer.GovernedHumanCorrectionReceipt;
                if (correction is null || receipt is null)
                        return false;

                string expectedLineage =
                        $"governed-human-correction:{correction.CorrelationId}";
                return observed.AttemptCount == job.AttemptCount &&
                    string.Equals(
                            observed.ProposalLineageId,
                            expectedLineage,
                            StringComparison.Ordinal) &&
                    string.Equals(
                            observed.ProposalFingerprintSha256,
                            SafeChangeProposalIdentity.Fingerprint(
                                    developer.DeveloperProposal),
                            StringComparison.OrdinalIgnoreCase) &&
                    (string.Equals(
                            observed.SafeChangeReceiptReference,
                            expectedLineage,
                            StringComparison.Ordinal) ||
                     observed.SafeChangeReceiptReference.Contains(
                            "human-correction/receipt-",
                            StringComparison.Ordinal)) &&
                    receipt.JobId == job.Id &&
                    receipt.RunId == observed.RunId &&
                    receipt.AttemptCount == job.AttemptCount &&
                    receipt.CorrelationId == correction.CorrelationId &&
                    receipt.Changes.Count ==
                            developer.DeveloperProposal.Changes.Count &&
                    receipt.Changes.All(change =>
                            observed.Entries.Any(entry =>
                                    string.Equals(
                                            entry.RelativePath,
                                            change.RelativePath,
                                            StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(
                                            entry.BeforeSha256,
                                            change.BeforeSha256,
                                            StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(
                                            entry.FinalSha256,
                                            change.AfterSha256,
                                            StringComparison.OrdinalIgnoreCase)));
        }

        private static bool ProposalMatchesObserved(
                ValidatedDeveloperProposal proposal,
                ObservedChangeManifest observed) =>
                proposal.Changes.Count > 0 &&
                proposal.Changes.All(change =>
                        observed.Entries.Any(entry =>
                                string.Equals(
                                        entry.RelativePath,
                                        change.RelativePath,
                                        StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(
                                        entry.FinalSha256,
                                        HashContent(change.Content),
                                        StringComparison.OrdinalIgnoreCase)));

        private static bool ReviewerMatchesEffectiveSource(
                ReviewerReview review,
                ReviewerEffectiveSourceSnapshot source)
        {
                if (review.DeterministicAcceptanceGate is null)
                        return false;

                Dictionary<string, string> reviewed = review
                        .DeterministicAcceptanceGate.Criteria
                        .Where(result =>
                                result.Criterion.Kind ==
                                    DeterministicCriterionKind.FileExists &&
                                result.Status ==
                                    DeterministicCriterionStatus.Pass &&
                                result.Evidence.StartsWith(
                                    "sha256:",
                                    StringComparison.Ordinal))
                        .ToDictionary(
                                result => result.Criterion.RelativePath,
                                result => result.Evidence[7..],
                                StringComparer.OrdinalIgnoreCase);

                return reviewed.Count == source.Files.Count &&
                    source.Files.All(file =>
                            reviewed.TryGetValue(
                                    file.RelativePath,
                                    out string? reviewedSha) &&
                            string.Equals(
                                    reviewedSha,
                                    file.Sha256,
                                    StringComparison.OrdinalIgnoreCase));
        }

        private static string HashContent(string content) =>
                Convert.ToHexString(SHA256.HashData(
                        Encoding.UTF8.GetBytes(content)))
                    .ToLowerInvariant();

        private async Task<ReviewerEffectiveSourceSnapshot?>
                CaptureReviewerEffectiveSourceAsync(
                        Job job,
                        Guid runId,
                        PlannerPlan plan,
                        ValidatedDeveloperProposal proposal,
                        ObservedChangeManifest observedChanges,
                        DeveloperProposalLineage lineage,
                        CancellationToken cancellationToken)
        {
                try
                {
                        ExecutionPlaneLifecycleResult recovery =
                                await _executionPlaneLifecycle.RecoverAsync(
                                        job.Id,
                                        job.ExternalId,
                                        cancellationToken);
                        if (!recovery.IsSuccess || recovery.Session is null ||
                            recovery.Session.Repository.JobId != job.Id ||
                            !string.Equals(
                                    recovery.Session.Repository.JobExternalId,
                                    job.ExternalId,
                                    StringComparison.Ordinal) ||
                            !string.Equals(
                                    recovery.Session.Repository.Head,
                                    job.BaseRepositoryHead,
                                    StringComparison.OrdinalIgnoreCase))
                                return null;

                        ReviewerEffectiveSourceSnapshotResult captured =
                                await _reviewerEffectiveSourceSnapshotService.CaptureAsync(
                                        new ReviewerEffectiveSourceSnapshotRequest
                                        {
                                                JobId = job.Id,
                                                RunId = runId,
                                                Repository = recovery.Session.Repository,
                                                Plan = plan,
                                                EffectiveProposal = proposal,
                                                ObservedChanges = observedChanges,
                                                EffectiveProposalLineage = lineage
                                        },
                                        cancellationToken);
                        return captured.IsSuccess ? captured.Snapshot : null;
                }
                catch (OperationCanceledException)
                        when (cancellationToken.IsCancellationRequested)
                {
                        throw;
                }
                catch
                {
                        return null;
                }
        }

        internal static bool HasActiveHumanReviewCorrection(
                JobState state,
                IReadOnlyList<JobTransition> transitions)
        {
                if (state is not (
                        JobState.Developing or
                        JobState.Building or
                        JobState.Testing or
                        JobState.Reviewing))
                        return false;

                JobTransition[] relevant = transitions
                        .Where(transition =>
                                transition.ToState == JobState.WaitingHuman ||
                                (transition.FromState == JobState.WaitingHuman &&
                                 transition.ToState == JobState.Developing))
                        .ToArray();

                if (relevant.Length == 0)
                        return false;

                DateTime latestCorrectionStart = relevant
                        .Where(transition =>
                                transition.FromState == JobState.WaitingHuman &&
                                transition.ToState == JobState.Developing)
                        .Select(transition => transition.OccurredOnUtc)
                        .DefaultIfEmpty(DateTime.MinValue)
                        .Max();

                if (latestCorrectionStart == DateTime.MinValue)
                        return false;

                return !relevant.Any(transition =>
                        transition.ToState == JobState.WaitingHuman &&
                        transition.OccurredOnUtc > latestCorrectionStart);
        }

        private async Task<StageRecoveryResult>
                CheckRecoveryEvidenceAsync(
                        Job job,
                        Guid runId,
                        RecoveryStage stage,
                        string correlationId,
                        CancellationToken cancellationToken)
        {
                try
                {
                        return await _stageRecoveryEvidenceService
                                .CheckAsync(
                                        new StageRecoveryRequest
                                        {
                                                JobId = job.Id,
                                                RunId = runId,
                                                Stage = stage,
                                                JobRequest =
                                                        job.Request,
                                                CorrelationId =
                                                        correlationId,
                                                AttemptCount = job.AttemptCount
                                        },
                                        cancellationToken);
                }
                catch (OperationCanceledException)
                        when (cancellationToken
                                .IsCancellationRequested)
                {
                        throw;
                }
                catch
                {
                        return StageRecoveryResult.Failure(
                                "STAGE_RECOVERY_CHECK_FAILED");
                }
        }

        private static string Hash<T>(T value) =>
                Convert.ToHexString(SHA256.HashData(
                        JsonSerializer.SerializeToUtf8Bytes(value)))
                    .ToLowerInvariant();

        private static JobOperationResult?
                RecoveryFailure(
                        StageRecoveryResult recovery)
        {
                return recovery.Status switch
                {
                        StageRecoveryStatus.NotCompleted =>
                                null,

                        StageRecoveryStatus.Completed =>
                                null,

                        StageRecoveryStatus.Cancelled =>
                                JobOperationResult.Failure(
                                        JobOperationKind.Cancelled,
                                        JobApplicationErrors
                                                .StageRecoveryFailed),

                        StageRecoveryStatus.InvalidEvidence =>
                                JobOperationResult.Failure(
                                        JobOperationKind.PermanentFailure,
                                        JobApplicationErrors
                                                .StageRecoveryFailed),

                        _ =>
                                JobOperationResult.Failure(
                                        JobOperationKind.RetryableFailure,
                                        JobApplicationErrors
                                                .StageRecoveryFailed)
                };
        }

        private static JobOperationKind
                MapDeveloperFailureKind(
                        DeveloperExecutionFailureKind kind) =>
                kind switch
                {
                        DeveloperExecutionFailureKind.Cancelled or
                        DeveloperExecutionFailureKind.AiCancelled =>
                                JobOperationKind.Cancelled,

                        DeveloperExecutionFailureKind.AiTimedOut =>
                                JobOperationKind.TimedOut,

                        DeveloperExecutionFailureKind.InvalidRequest or
                        DeveloperExecutionFailureKind.PlanningPlanInvalid or
                        DeveloperExecutionFailureKind.ContextPackageInvalid or
                        DeveloperExecutionFailureKind.ContextTooLarge or
                        DeveloperExecutionFailureKind.AiRejected or
                        DeveloperExecutionFailureKind.AiInvalidResponse or
                        DeveloperExecutionFailureKind.PolicyRejected =>
                                JobOperationKind.PermanentFailure,

                        _ => JobOperationKind.RetryableFailure
                };

        private static JobOperationKind
                MapSafeChangeFailureKind(
                        SafeChangeApplicationFailureKind kind) =>
                kind switch
                {
                        SafeChangeApplicationFailureKind.Cancelled =>
                                JobOperationKind.Cancelled,

                        SafeChangeApplicationFailureKind.InvalidRequest or
                        SafeChangeApplicationFailureKind.UnsafeWorkspace or
                        SafeChangeApplicationFailureKind.InvalidPath or
                        SafeChangeApplicationFailureKind.ProtectedPath or
                        SafeChangeApplicationFailureKind.PreconditionFailed or
                        SafeChangeApplicationFailureKind.DestinationConflict or
                        SafeChangeApplicationFailureKind.SymlinkDetected or
                        SafeChangeApplicationFailureKind.LimitExceeded =>
                                JobOperationKind.PermanentFailure,

                        _ => JobOperationKind.RetryableFailure
                };

        private static JobOperationKind
                MapPlanningFailureKind(
                        PlanningExecutionFailureKind kind) =>
                kind switch
                {
                        PlanningExecutionFailureKind.Cancelled or
                        PlanningExecutionFailureKind.AiCancelled =>
                                JobOperationKind.Cancelled,

                        PlanningExecutionFailureKind.AiTimedOut =>
                                JobOperationKind.TimedOut,

                        PlanningExecutionFailureKind.InvalidRequest or
                        PlanningExecutionFailureKind.ContextPackageInvalid or
                        PlanningExecutionFailureKind.ContextTooLarge or
                        PlanningExecutionFailureKind.AiRejected or
                        PlanningExecutionFailureKind.AiInvalidResponse =>
                                JobOperationKind.PermanentFailure,

                        _ =>
                                JobOperationKind.RetryableFailure
                };

        private static JobOperationKind
                MapTestFailureKind(
                        TestExecutionFailureKind failureKind)
        {
                return failureKind switch
                {
                        TestExecutionFailureKind.Cancelled =>
                                JobOperationKind.Cancelled,

                        TestExecutionFailureKind.TimedOut =>
                                JobOperationKind.TimedOut,

                        TestExecutionFailureKind.InvalidRequest or
                        TestExecutionFailureKind.ToolRejected or
                        TestExecutionFailureKind.TestResultsUnsafe =>
                                JobOperationKind.PermanentFailure,

                        _ =>
                                JobOperationKind.RetryableFailure
                };
        }

        private static JobOperationKind
                MapBuildFailureKind(
                        BuildExecutionFailureKind failureKind)
        {
                return failureKind switch
                {
                        BuildExecutionFailureKind.Cancelled =>
                                JobOperationKind.Cancelled,

                        BuildExecutionFailureKind.TimedOut =>
                                JobOperationKind.TimedOut,

                        BuildExecutionFailureKind.InvalidRequest or
                        BuildExecutionFailureKind.ToolRejected =>
                                JobOperationKind.PermanentFailure,

                        _ =>
                                JobOperationKind.RetryableFailure
                };
        }

        private static JobOperationKind
                MapRestoreFailureKind(
                        RestoreExecutionFailureKind failureKind)
        {
                return failureKind switch
                {
                        RestoreExecutionFailureKind.Cancelled =>
                                JobOperationKind.Cancelled,

                        RestoreExecutionFailureKind.TimedOut =>
                                JobOperationKind.TimedOut,

                        RestoreExecutionFailureKind.InvalidRequest or
                        RestoreExecutionFailureKind.ToolRejected =>
                                JobOperationKind.PermanentFailure,

                        _ =>
                                JobOperationKind.RetryableFailure
                };
        }

        private static JobOperationKind
                MapContextFailureKind(
                        ContextGenerationFailureKind failureKind)
        {
                return failureKind switch
                {
                        ContextGenerationFailureKind.Cancelled =>
                                JobOperationKind.Cancelled,

                        ContextGenerationFailureKind.TimedOut =>
                                JobOperationKind.TimedOut,

                        ContextGenerationFailureKind.InvalidRequest or
                        ContextGenerationFailureKind.RepositoryMismatch or
                        ContextGenerationFailureKind.SensitiveContentDetected or
                        ContextGenerationFailureKind.PackageLimitExceeded or
                        ContextGenerationFailureKind.InvalidPackage =>
                                JobOperationKind.PermanentFailure,

                        _ =>
                                JobOperationKind.RetryableFailure
                };
        }

        private async Task<JobOperationResult>
                AdvanceStateOnlyAsync(
                        Job job,
                        string actor,
                        string correlationId,
                        CancellationToken cancellationToken)
        {
                JobState? nextState =
                        _stateMachine
                                .DetermineNextAutomaticState(
                                        job.State);

                if (!nextState.HasValue)
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.InvalidTransition,
                                JobApplicationErrors.NoNextState);
                }

                return await TransitionAndSaveAsync(
                        job,
                        nextState.Value,
                        $"Advance to {nextState.Value}.",
                        actor,
                        correlationId,
                        cancellationToken);
        }

        private async Task<JobOperationResult>
                TransitionAndSaveAsync(
                        Job job,
                        JobState targetState,
                        string reason,
                        string actor,
                        string correlationId,
                        CancellationToken cancellationToken)
        {
                Result transition =
                        job.TransitionTo(
                                targetState,
                                _clock.UtcNow,
                                reason,
                                actor,
                                correlationId);

                if (transition.IsFailure)
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.InvalidTransition,
                                transition.Error);
                }

                await _unitOfWork
                        .SaveChangesAsync(
                                cancellationToken);

                return JobOperationResult.Success();
        }

        public async Task<JobOperationResult>
                MarkRetryPendingAsync(
                        Guid jobId,
                        string reason,
                        string actor,
                        string correlationId,
                        CancellationToken cancellationToken =
                                default(CancellationToken))
        {
                Job? job =
                        await _jobRepository
                                .GetByIdAsync(
                                        jobId,
                                        cancellationToken);

                if (job is null)
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.NotFound);
                }

                if (job.State == JobState.RetryPending)
                {
                        return JobOperationResult.Success();
                }

                if (_stateMachine.IsTerminal(job.State))
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.InvalidTransition,
                                JobApplicationErrors.TerminalJob);
                }

                Result transition =
                        job.TransitionTo(
                                JobState.RetryPending,
                                _clock.UtcNow,
                                reason,
                                actor,
                                correlationId);

                if (transition.IsFailure)
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.InvalidTransition,
                                transition.Error);
                }

                await _unitOfWork
                        .SaveChangesAsync(
                                cancellationToken);

                return JobOperationResult.Success();
        }

        public async Task<JobOperationResult>
                ResumeAsync(
                        Guid jobId,
                        string reason,
                        string actor,
                        string correlationId,
                        CancellationToken cancellationToken =
                                default(CancellationToken))
        {
                Job? job =
                        await _jobRepository
                                .GetByIdAsync(
                                        jobId,
                                        cancellationToken);

                if (job is null)
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.PermanentFailure,
                                JobApplicationErrors.NotFound);
                }

                Result resume =
                        job.Resume(
                                _clock.UtcNow,
                                reason,
                                actor,
                                correlationId);

                if (resume.IsFailure)
                {
                        return JobOperationResult.Failure(
                                JobOperationKind.InvalidTransition,
                                resume.Error);
                }

                await _unitOfWork
                        .SaveChangesAsync(
                                cancellationToken);

                return JobOperationResult.Success();
        }
}
