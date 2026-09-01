using System;
using System.Threading;
using System.Threading.Tasks;
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Execution;
using Kronxy.Application.Context;
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

        private readonly IStageRecoveryEvidenceService _stageRecoveryEvidenceService;

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
                IStageRecoveryEvidenceService stageRecoveryEvidenceService)
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

                _stageRecoveryEvidenceService =
                        stageRecoveryEvidenceService ??
                        throw new ArgumentNullException(
                                nameof(stageRecoveryEvidenceService));
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
                        return await TransitionAndSaveAsync(
                                job,
                                JobState.Planning,
                                "Context evidence recovered; generation was not repeated.",
                                actor,
                                correlationId,
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

                return await TransitionAndSaveAsync(
                        job,
                        JobState.Planning,
                        "Context package generated from the pinned repository revision.",
                        actor,
                        correlationId,
                        cancellationToken);
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

                StageRecoveryResult recoveredBuild =
                        await CheckRecoveryEvidenceAsync(
                                job,
                                runId,
                                RecoveryStage.Build,
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

                StageRecoveryResult recoveredTest =
                        await CheckRecoveryEvidenceAsync(
                                job,
                                runId,
                                RecoveryStage.Test,
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
                        return StageRecoveryResult.Failure(
                                "STAGE_RECOVERY_CHECK_FAILED");
                }
        }

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
