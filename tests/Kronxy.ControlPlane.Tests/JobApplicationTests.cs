using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Execution;
using Kronxy.Application.Context;
using Kronxy.Application.Repositories;
using Kronxy.Application.Workspaces;
using Kronxy.Application.Jobs;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Jobs;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class JobApplicationTests
{
	private sealed record Fixture(
	        FakeJobRepository Repository,
	        FakeUnitOfWork UnitOfWork,
	        FakeClock Clock,
	        JobService Service,
	        JobOrchestrator Orchestrator,
	        FakeSourceRevisionProvider SourceRevision,
	        FakeExecutionPlaneLifecycle ExecutionPlane,
                FakeContextGenerationService Context,
                FakeRestoreExecutionService Restore,
                FakeExecutionTargetProvider Target,
                FakeBuildExecutionService Build,
                FakeTestExecutionService Test,
                FakePlanningExecutionService Planning,
                FakeDeveloperExecutionService Developer,
                FakeSafeChangeApplier SafeChange,
                FakeObservedChangeEvidenceService Observed,
                FakeStageRecoveryEvidenceService RecoveryEvidence,
                FakeReviewerExecutionService Reviewer,
                FakeReviewDecisionPolicy ReviewPolicy,
                FakeArtifactStore Artifacts,
                FakeArtifactMetadataRepository ArtifactMetadata);

        private sealed class FakeArtifactMetadataRepository : IArtifactMetadataRepository
        {
                public List<ArtifactRecord> Records { get; } = [];

                public Task AddAsync(
                        ArtifactRecord artifact,
                        CancellationToken cancellationToken = default)
                {
                        Records.Add(artifact);
                        return Task.CompletedTask;
                }

                public Task<ArtifactRecord?> GetByIdAsync(
                        Guid artifactId,
                        CancellationToken cancellationToken = default) =>
                        Task.FromResult(Records.SingleOrDefault(
                                artifact => artifact.ArtifactId == artifactId));

                public Task<IReadOnlyList<ArtifactRecord>> GetByJobAndRunAsync(
                        Guid jobId,
                        Guid runId,
                        CancellationToken cancellationToken = default) =>
                        Task.FromResult<IReadOnlyList<ArtifactRecord>>(Records
                                .Where(artifact => artifact.JobId == jobId &&
                                        artifact.RunId == runId)
                                .ToArray());
        }

        private sealed class FakeDevelopmentAnalysisService(
                DevelopmentChangeClassification classification) : IDevelopmentAnalysisService
        {
                public Task<DevelopmentAnalysisResult> AnalyzeAsync(
                        DevelopmentAnalysisRequest request,
                        CancellationToken cancellationToken = default)
                {
                        bool alreadySatisfied = classification == DevelopmentChangeClassification.AlreadySatisfied;
                        bool decisionRequired = classification is DevelopmentChangeClassification.ArchitectureConflict or DevelopmentChangeClassification.Unknown;
                        var analysis = new DevelopmentAnalysis
                        {
                                JobId = request.JobId, RunId = request.RunId, AttemptCount = request.AttemptCount,
                                RequestIdentity = "test", TargetSymbols = [], ExistingDeclarations = [],
                                PrimaryClassification = classification, ImpactedLayers = ["Domain"],
                                RequestedScope = "Domain-only", RequiredScope = decisionRequired ? "Cross-layer" : "Domain",
                                ScopeCompatible = !decisionRequired, BreakingContracts = [],
                                ArchitectureDecisionRequired = decisionRequired,
                                DeveloperExecutionAllowed = !decisionRequired && !alreadySatisfied,
                                Evidence = [], FilesInspected = [], AnalysisVersion = "test"
                        };
                        return Task.FromResult(DevelopmentAnalysisResult.Success(analysis, new ArtifactRecord
                        {
                                ArtifactId = Guid.NewGuid(), JobId = request.JobId, RunId = request.RunId,
                                ArtifactType = ArtifactType.DevelopmentAnalysis,
                                RelativePath = "development-analysis/analysis.json", Sha256 = new string('a', 64),
                                SizeBytes = 1, CreatedAtUtc = DateTimeOffset.UtcNow, CorrelationId = request.CorrelationId
                        }));
                }
        }

        private sealed class FakeArtifactStore : IArtifactStore
        {
                public List<ArtifactWriteRequest> Writes { get; } = [];
                public Task<ArtifactWriteResult> WriteAsync(
                        ArtifactWriteRequest request,
                        CancellationToken cancellationToken = default)
                {
                        Writes.Add(request);
                        return Task.FromResult(ArtifactWriteResult.Success(new ArtifactRecord
                        {
                                ArtifactId = Guid.NewGuid(), JobId = request.JobId,
                                RunId = request.RunId, ArtifactType = request.ArtifactType,
                                RelativePath = "human-review/changes-required.json",
                                Sha256 = new string('a', 64), SizeBytes = request.Content.Length,
                                CreatedAtUtc = DateTimeOffset.UtcNow,
                                CorrelationId = request.CorrelationId
                        }));
                }
        }

	private sealed class FakeJobRepository : IJobRepository
	{
		private readonly List<Job> _items = new List<Job>();

		public IReadOnlyList<Job> Items => _items;

		public Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default(CancellationToken))
		{
			return Task.FromResult(_items.SingleOrDefault((Job job) => job.Id == id));
		}

		public Task<Job?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default(CancellationToken))
		{
			return Task.FromResult(_items.SingleOrDefault((Job job) => string.Equals(job.ExternalId, externalId, StringComparison.Ordinal)));
		}

		public void Add(Job job)
		{
			_items.Add(job);
		}
	}

	private sealed class FakeUnitOfWork : IUnitOfWork
	{
		public int SaveCount { get; private set; }

		public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			SaveCount++;
			return Task.FromResult(1);
		}
	}

	private sealed class FakeClock : IDateTimeProvider
	{
		public DateTime UtcNow { get; set; }

		public FakeClock(DateTime utcNow)
		{
			UtcNow = utcNow;
		}
	}

	private sealed class FakeJobIdGenerator : IJobIdGenerator
	{
		private int _sequence;

		public Guid NewId()
		{
			_sequence++;
			byte[] array = new byte[16];
			BitConverter.GetBytes(_sequence).CopyTo(array, 0);
			return new Guid(array);
		}

		public string NewExternalId()
		{
			_sequence++;
			return $"KRX-{_sequence:000000}";
		}
	}

	private sealed class FakeSourceRevisionProvider :
	        ISourceRevisionProvider
	{
	        public string Head { get; set; } =
	                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

	        public bool Fail { get; set; }

	        public int CallCount { get; private set; }

	        public Task<SourceRevisionResult>
	                GetAuthoritativeHeadAsync(
	                        CancellationToken cancellationToken =
	                                default(CancellationToken))
	        {
	                CallCount++;

	                if (Fail)
	                {
	                        return Task.FromResult(
	                                SourceRevisionResult.Failure(
	                                        "TEST_SOURCE_FAILURE"));
	                }

	                return Task.FromResult(
	                        SourceRevisionResult.Success(
	                                Head));
	        }
	}

    private sealed class FakeExecutionPlaneLifecycle :
            IExecutionPlaneLifecycle
    {
            public bool FailPrepare { get; set; }

            public bool FailRecover { get; set; }

            public string? PreparedHeadOverride { get; set; }

            public int PrepareCount { get; private set; }

            public int RecoverCount { get; private set; }

            public string? LastExpectedHead { get; private set; }

            private ExecutionPlaneSession? _lastSession;

            public Task<ExecutionPlaneLifecycleResult>
                    PrepareAsync(
                            Guid jobId,
                            string jobExternalId,
                            string expectedHead,
                            CancellationToken cancellationToken =
                                    default(CancellationToken))
            {
                    PrepareCount++;
                    LastExpectedHead = expectedHead;

                    if (FailPrepare)
                    {
                            return Task.FromResult(
                                    ExecutionPlaneLifecycleResult.Failure(
                                            ExecutionPlaneFailureKind
                                                    .RepositoryFailure,
                                            "TEST_EXECUTION_FAILURE"));
                    }

                    WorkspaceHandle workspace =
                            new WorkspaceHandle(
                                    jobId,
                                    jobExternalId,
                                    "/tmp/kronxy-test-workspace",
                                    WorkspaceOperationKind.Created);

                    RepositoryWorktreeHandle repository =
                            new RepositoryWorktreeHandle(
                                    jobId,
                                    jobExternalId,
                                    workspace.Path,
                                    workspace.Path + "/repository",
                                    "kronxy/jobs/test",
                                    PreparedHeadOverride ??
                                            expectedHead,
                                    RepositoryWorktreeOperationKind
                                            .Created);

                    _lastSession =
                            new ExecutionPlaneSession(
                                    workspace,
                                    repository);

                    return Task.FromResult(
                            ExecutionPlaneLifecycleResult.Success(
                                    _lastSession));
            }

            public Task<ExecutionPlaneLifecycleResult>
                    RecoverAsync(
                            Guid jobId,
                            string jobExternalId,
                            CancellationToken cancellationToken =
                                    default(CancellationToken))
            {
                    RecoverCount++;

                    if (FailRecover ||
                        _lastSession is null)
                    {
                            return Task.FromResult(
                                    ExecutionPlaneLifecycleResult.Failure(
                                            ExecutionPlaneFailureKind
                                                    .WorkspaceFailure,
                                            "TEST_RECOVERY_FAILURE"));
                    }

                    WorkspaceHandle workspace =
                            _lastSession.Workspace with
                            {
                                    Operation =
                                            WorkspaceOperationKind
                                                    .Recovered
                            };

                    RepositoryWorktreeHandle repository =
                            _lastSession.Repository with
                            {
                                    Operation =
                                            RepositoryWorktreeOperationKind
                                                    .Recovered
                            };

                    _lastSession =
                            new ExecutionPlaneSession(
                                    workspace,
                                    repository);

                    return Task.FromResult(
                            ExecutionPlaneLifecycleResult.Success(
                                    _lastSession));
            }

            public Task<ExecutionPlaneLifecycleResult>
                    CleanupAsync(
                            Guid jobId,
                            string jobExternalId,
                            CancellationToken cancellationToken =
                                    default(CancellationToken))
            {
                    return Task.FromResult(
                            ExecutionPlaneLifecycleResult.Failure(
                                    ExecutionPlaneFailureKind
                                            .WorkspaceFailure,
                                    "TEST_NOT_USED"));
            }
    }


        private sealed class FakeContextGenerationService :
                IContextGenerationService
        {
                public ContextGenerationFailureKind FailureKind { get; set; }

                public int CallCount { get; private set; }

                public ContextGenerationRequest? LastRequest { get; private set; }

                public Task<ContextGenerationResult> GenerateAsync(
                        ContextGenerationRequest request,
                        CancellationToken cancellationToken =
                                default(CancellationToken))
                {
                        cancellationToken.ThrowIfCancellationRequested();

                        CallCount++;
                        LastRequest = request;

                        if (FailureKind !=
                            ContextGenerationFailureKind.None)
                        {
                                return Task.FromResult(
                                        ContextGenerationResult.Failure(
                                                FailureKind,
                                                "TEST_CONTEXT_FAILURE"));
                        }

                        return Task.FromResult(
                                ContextGenerationResult.Success(
                                        new ContextPackageArtifact
                                        {
                                                PackageId =
                                                        "test-package",

                                                RelativePath =
                                                        "context/context.zip",

                                                Sha256 =
                                                        new string('a', 64),

                                                SizeBytes =
                                                        1024,

                                                EntryCount =
                                                        1,

                                                GeneratorVersion =
                                                        "test",

                                                CreatedAtUtc =
                                                        new DateTimeOffset(
                                                                2026,
                                                                8,
                                                                29,
                                                                12,
                                                                0,
                                                                0,
                                                                TimeSpan.Zero)
                                        }));
                }
        }

	private static readonly DateTime UtcNow = new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public async Task Create_WithSameExternalId_IsIdempotent()
	{
		Fixture fixture = CreateFixture();
		Result<Job> first = await fixture.Service.CreateAsync("same request", "KRX-000001");
		Result<Job> second = await fixture.Service.CreateAsync("same request", "KRX-000001");
		Assert.True(first.IsSuccess);
		Assert.True(second.IsSuccess);
		Assert.Same((object)first.Value, (object)second.Value);
		Assert.Single<Job>((IEnumerable<Job>)fixture.Repository.Items);
		Assert.Equal<int>(1, fixture.UnitOfWork.SaveCount);
	}

	[Fact]
	public async Task Create_WithInvalidConfiguration_FailsClosed()
	{
		FakeJobRepository repository = new FakeJobRepository();
		FakeUnitOfWork unitOfWork = new FakeUnitOfWork();
		FakeClock clock = new FakeClock(UtcNow);
		FakeJobIdGenerator generator = new FakeJobIdGenerator();
		JobControlOptions options = new JobControlOptions
		{
			MaxJobDuration = TimeSpan.Zero,
			MaxAttempts = 0,
			MaxAgentIterations = 0,
			MaxAiCalls = 0
		};
		JobService service = new JobService(repository, unitOfWork, clock, generator, options);
		Result<Job> result = await service.CreateAsync("request", "KRX-FAIL");
		Assert.True(result.IsFailure);
		Assert.Equal<Error>(JobApplicationErrors.InvalidConfiguration, result.Error);
		Assert.Empty((IEnumerable)repository.Items);
		Assert.Equal<int>(0, unitOfWork.SaveCount);
	}

	[Fact]
	public async Task Cancel_Repeated_IsIdempotent()
	{
		Fixture fixture = CreateFixture();
		Result<Job> created = await fixture.Service.CreateAsync("request", "KRX-CANCEL");
		JobOperationResult first = await fixture.Service.CancelAsync(created.Value.Id, "human", "corr-cancel");
		int transitionCount = created.Value.Transitions.Count;
		int saveCount = fixture.UnitOfWork.SaveCount;
		JobOperationResult second = await fixture.Service.CancelAsync(created.Value.Id, "human", "corr-cancel");
		Assert.True(first.IsSuccess);
		Assert.True(second.IsSuccess);
		Assert.Equal<JobState>(JobState.Cancelled, created.Value.State);
		Assert.Equal<int>(transitionCount, created.Value.Transitions.Count);
		Assert.Equal<int>(saveCount, fixture.UnitOfWork.SaveCount);
	}

	[Fact]
	public async Task Advance_MovesOneAuthorizedState()
	{
		Fixture fixture = CreateFixture();
		Result<Job> created = await fixture.Service.CreateAsync("request", "KRX-ADVANCE");
		Assert.True((await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "corr-advance")).IsSuccess);
		Assert.Equal<JobState>(JobState.ContextBuilding, created.Value.State);
	}

	[Fact]
	public async Task RetryPending_IncrementsAttemptExactlyOnce()
	{
		Fixture fixture = CreateFixture();
		Result<Job> created = await fixture.Service.CreateAsync("request", "KRX-RETRY");
		await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "corr-1");
		Assert.Equal<int>(1, created.Value.AttemptCount);
		Assert.True((await fixture.Orchestrator.MarkRetryPendingAsync(created.Value.Id, "transient failure", "orchestrator", "corr-retry")).IsSuccess);
		Assert.Equal<int>(2, created.Value.AttemptCount);
		Assert.Equal<JobState>(JobState.RetryPending, created.Value.State);
		int transitionCount = created.Value.Transitions.Count;
		Assert.True((await fixture.Orchestrator.MarkRetryPendingAsync(created.Value.Id, "transient failure", "orchestrator", "corr-retry")).IsSuccess);
		Assert.Equal<int>(2, created.Value.AttemptCount);
		Assert.Equal<int>(transitionCount, created.Value.Transitions.Count);
	}

	[Fact]
	public async Task Retry_StopsAtConfiguredAttemptLimit()
	{
		Fixture fixture = CreateFixture(2);
		Result<Job> created = await fixture.Service.CreateAsync("request", "KRX-LIMIT");
		await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "corr");
		Assert.True((await fixture.Orchestrator.MarkRetryPendingAsync(created.Value.Id, "failure", "orchestrator", "corr")).IsSuccess);
		Assert.Equal<int>(2, created.Value.AttemptCount);
		Assert.True((await fixture.Orchestrator.ResumeAsync(created.Value.Id, "retry", "orchestrator", "corr")).IsSuccess);
		JobOperationResult secondRetry = await fixture.Orchestrator.MarkRetryPendingAsync(created.Value.Id, "failure again", "orchestrator", "corr");
		Assert.False(secondRetry.IsSuccess);
		Assert.Equal<JobOperationKind>(JobOperationKind.InvalidTransition, secondRetry.Kind);
		Assert.Equal<Error>(JobErrors.MaxAttemptsExceeded, secondRetry.Error);
		Assert.Equal<int>(2, created.Value.AttemptCount);
	}

	[Fact]
	public async Task Advance_WhenDurationExceeded_TimesOutJob()
	{
		Fixture fixture = CreateFixture(3, TimeSpan.FromMinutes(30.0));
		Result<Job> created = await fixture.Service.CreateAsync("request", "KRX-TIMEOUT");
		await fixture.Orchestrator.AdvanceAsync(
			created.Value.Id, "orchestrator", "corr-start");
		created.Value.BeginActiveExecution(UtcNow);
		fixture.Clock.UtcNow = UtcNow.AddMinutes(31.0);
		JobOperationResult result = await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "corr-timeout");
		Assert.False(result.IsSuccess);
		Assert.Equal<JobOperationKind>(JobOperationKind.TimedOut, result.Kind);
		Assert.Equal<JobState>(JobState.TimedOut, created.Value.State);
		Assert.True(created.Value.IsTerminal);
		Assert.NotNull<DateTime>(created.Value.CompletedOnUtc);
	}

	[Fact]
	public async Task Planning_retry_after_long_inactive_pause_starts_without_timeout()
	{
		Fixture fixture = CreateFixture(3, TimeSpan.FromMinutes(30.0));
		Result<Job> created = await fixture.Service.CreateAsync("request", "KRX-LATE-PLANNING");
		await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "corr-1");
		await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "corr-2");
		Assert.Equal(JobState.Planning, created.Value.State);
		fixture.Clock.UtcNow = UtcNow.AddHours(20.0);

		JobOperationResult result = await fixture.Orchestrator.AdvanceAsync(
			created.Value.Id, "orchestrator", "corr-late-retry");

		Assert.True(result.IsSuccess);
		Assert.Equal(JobState.WorkspacePreparing, created.Value.State);
		Assert.Equal(1, created.Value.AttemptCount);
		Assert.Equal(1, fixture.Planning.CallCount);
		Assert.Null(created.Value.ActiveExecutionStartedOnUtc);
		Assert.Equal(fixture.Clock.UtcNow, created.Value.LastActiveProgressOnUtc);
	}

	[Fact]
	public async Task Rejected_planning_invocation_records_governed_progress_and_closes_lease()
	{
		Fixture fixture = CreateFixture(3, TimeSpan.FromMinutes(30.0));
		Result<Job> created = await fixture.Service.CreateAsync("request", "KRX-PLANNING-REJECTED");
		await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "corr-1");
		await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "corr-2");
		fixture.Planning.Result = PlanningExecutionResult.Failure(
			PlanningExecutionFailureKind.AiInvalidResponse,
			"PLANNING_PATH_COHERENCE_INVALID");
		fixture.Clock.UtcNow = UtcNow.AddHours(20.0);

		JobOperationResult result = await fixture.Orchestrator.AdvanceAsync(
			created.Value.Id, "orchestrator", "corr-rejected");

		Assert.False(result.IsSuccess);
		Assert.Equal(JobState.Planning, created.Value.State);
		Assert.Null(created.Value.ActiveExecutionStartedOnUtc);
		Assert.Equal(fixture.Clock.UtcNow, created.Value.LastActiveProgressOnUtc);
	}

	[Fact]
	public void DetermineNextState_IsDeterministic()
	{
		Fixture fixture = CreateFixture();
		Assert.Equal<JobState?>((JobState?)JobState.ContextBuilding, fixture.Orchestrator.DetermineNextState(JobState.Created));
		Assert.Equal<JobState?>((JobState?)JobState.Planning, fixture.Orchestrator.DetermineNextState(JobState.ContextBuilding));
		Assert.Equal<JobState?>((JobState?)JobState.WaitingHuman, fixture.Orchestrator.DetermineNextState(JobState.Reviewing));
		Assert.Null<JobState>(fixture.Orchestrator.DetermineNextState(JobState.WaitingHuman));
		Assert.Null<JobState>(fixture.Orchestrator.DetermineNextState(JobState.Completed));
	}

    [Fact]
    public async Task Planning_ContextAlreadyBuilt_StopsAtWorkspacePreparing()
    {
            Fixture fixture = CreateFixture();

            Result<Job> created =
                    await fixture.Service.CreateAsync(
                            "request",
                            "KRX-PLAN");

            await fixture.Orchestrator.AdvanceAsync(
                    created.Value.Id,
                    "orchestrator",
                    "corr-created");

            JobOperationResult context =
                    await fixture.Orchestrator.AdvanceAsync(
                            created.Value.Id,
                            "orchestrator",
                            "corr-context");

            Assert.True(context.IsSuccess);

            Assert.Equal(
                    JobState.Planning,
                    created.Value.State);

            Assert.Equal(
                    fixture.SourceRevision.Head,
                    created.Value.BaseRepositoryHead);

            Assert.Equal(
                    1,
                    fixture.SourceRevision.CallCount);

            Assert.Equal(
                    1,
                    fixture.ExecutionPlane.PrepareCount);

            Assert.Equal(
                    1,
                    fixture.Context.CallCount);

            JobOperationResult result =
                    await fixture.Orchestrator.AdvanceAsync(
                            created.Value.Id,
                            "orchestrator",
                            "corr-planning");

            Assert.True(result.IsSuccess);

            Assert.Equal(
                    JobState.WorkspacePreparing,
                    created.Value.State);

            Assert.Equal(
                    1,
                    fixture.ExecutionPlane.PrepareCount);

            Assert.Equal(
                    0,
                    fixture.ExecutionPlane.RecoverCount);

            Assert.Equal(
                    1,
                    fixture.Context.CallCount);
        Assert.Equal(
            1,
            fixture.Planning.CallCount);

    }


    [Fact]
    public async Task Planning_AiFailure_DoesNotAdvance()
    {
        Fixture fixture = CreateFixture();

        JobLimits limits =
            JobLimits.Create(
                TimeSpan.FromHours(2),
                3,
                10,
                20)
            .Value;

        Job job =
            Job.Create(
                Guid.NewGuid(),
                "JOB-PLANNING-FAIL",
                "Implement safely.",
                limits,
                UtcNow)
            .Value;

        job.PinBaseRepositoryHead(
            "1111111111111111111111111111111111111111");

        job.TransitionTo(
            JobState.ContextBuilding,
            UtcNow,
            "Context requested.",
            "test",
            "corr");

        job.TransitionTo(
            JobState.Planning,
            UtcNow,
            "Context ready.",
            "test",
            "corr");

        fixture.Repository.Add(job);

        fixture.Planning.Result =
            PlanningExecutionResult.Failure(
                PlanningExecutionFailureKind.AiUnavailable,
                "AI_UNAVAILABLE");

        JobOperationResult result =
            await fixture.Orchestrator.AdvanceAsync(
                job.Id,
                "test",
                "corr-planning");

        Assert.False(result.IsSuccess);

        Assert.Equal(
            JobOperationKind.RetryableFailure,
            result.Kind);

        Assert.Equal(
            JobState.Planning,
            job.State);

        Assert.Equal(
            1,
            fixture.Planning.CallCount);
    }


    [Fact]
    public async Task WorkspacePreparing_RecoversPinnedRevision_ThenMovesToDeveloping()
    {
            Fixture fixture = CreateFixture();

            Result<Job> created =
                    await fixture.Service.CreateAsync(
                            "request",
                            "KRX-WORKSPACE");

            await fixture.Orchestrator.AdvanceAsync(
                    created.Value.Id,
                    "orchestrator",
                    "corr-created");

            await fixture.Orchestrator.AdvanceAsync(
                    created.Value.Id,
                    "orchestrator",
                    "corr-context");

            await fixture.Orchestrator.AdvanceAsync(
                    created.Value.Id,
                    "orchestrator",
                    "corr-planning");

            string pinned =
                    created.Value.BaseRepositoryHead!;

            JobOperationResult result =
                    await fixture.Orchestrator.AdvanceAsync(
                            created.Value.Id,
                            "orchestrator",
                            "corr-workspace");

            Assert.True(result.IsSuccess);

            Assert.Equal(
                    JobState.Developing,
                    created.Value.State);

            Assert.Equal(
                    1,
                    fixture.ExecutionPlane.PrepareCount);

            Assert.Equal(
                    1,
                    fixture.ExecutionPlane.RecoverCount);

            Assert.Equal(
                    pinned,
                    fixture.ExecutionPlane.LastExpectedHead);

            Assert.Equal(
                    pinned,
                    created.Value.BaseRepositoryHead);

            Assert.Equal(
                    1,
                    fixture.SourceRevision.CallCount);

            Assert.Equal(
                    1,
                    fixture.Context.CallCount);
    }


    [Fact]
    public async Task ContextBuilding_SourceRevisionFailure_DoesNotAdvanceOrPersist()
    {
            Fixture fixture = CreateFixture();

            Result<Job> created =
                    await fixture.Service.CreateAsync(
                            "request",
                            "KRX-SOURCE-FAIL");

            await fixture.Orchestrator.AdvanceAsync(
                    created.Value.Id,
                    "orchestrator",
                    "corr-created");

            Assert.Equal(
                    JobState.ContextBuilding,
                    created.Value.State);

            int saveCount =
                    fixture.UnitOfWork.SaveCount;

            int transitionCount =
                    created.Value.Transitions.Count;

            fixture.SourceRevision.Fail = true;

            JobOperationResult result =
                    await fixture.Orchestrator.AdvanceAsync(
                            created.Value.Id,
                            "orchestrator",
                            "corr-context");

            Assert.False(result.IsSuccess);

            Assert.Equal(
                    JobOperationKind.RetryableFailure,
                    result.Kind);

            Assert.Equal(
                    JobApplicationErrors.SourceRevisionUnavailable,
                    result.Error);

            Assert.Equal(
                    JobState.ContextBuilding,
                    created.Value.State);

            Assert.Null(
                    created.Value.BaseRepositoryHead);

            Assert.Equal(
                    0,
                    fixture.ExecutionPlane.PrepareCount);

            Assert.Equal(
                    0,
                    fixture.Context.CallCount);

            Assert.Equal(
                    saveCount + 2,
                    fixture.UnitOfWork.SaveCount);

            Assert.Equal(
                    transitionCount,
                    created.Value.Transitions.Count);
    }


    [Fact]
    public async Task WorkspaceRecoveryFailure_PreservesStateAndPinnedRevision()
    {
            Fixture fixture = CreateFixture();

            Result<Job> created =
                    await fixture.Service.CreateAsync(
                            "request",
                            "KRX-WORKSPACE-FAIL");

            await fixture.Orchestrator.AdvanceAsync(
                    created.Value.Id,
                    "orchestrator",
                    "corr-created");

            await fixture.Orchestrator.AdvanceAsync(
                    created.Value.Id,
                    "orchestrator",
                    "corr-context");

            await fixture.Orchestrator.AdvanceAsync(
                    created.Value.Id,
                    "orchestrator",
                    "corr-planning");

            string pinned =
                    created.Value.BaseRepositoryHead!;

            int saveCount =
                    fixture.UnitOfWork.SaveCount;

            int transitionCount =
                    created.Value.Transitions.Count;

            fixture.ExecutionPlane.FailRecover = true;

            JobOperationResult result =
                    await fixture.Orchestrator.AdvanceAsync(
                            created.Value.Id,
                            "orchestrator",
                            "corr-workspace");

            Assert.False(result.IsSuccess);

            Assert.Equal(
                    JobOperationKind.RetryableFailure,
                    result.Kind);

            Assert.Equal(
                    JobApplicationErrors
                            .ExecutionPlanePreparationFailed,
                    result.Error);

            Assert.Equal(
                    JobState.WorkspacePreparing,
                    created.Value.State);

            Assert.Equal(
                    pinned,
                    created.Value.BaseRepositoryHead);

            Assert.Equal(
                    1,
                    fixture.ExecutionPlane.PrepareCount);

            Assert.Equal(
                    1,
                    fixture.ExecutionPlane.RecoverCount);

            Assert.Equal(
                    saveCount + 2,
                    fixture.UnitOfWork.SaveCount);

            Assert.Equal(
                    transitionCount,
                    created.Value.Transitions.Count);
    }


    [Fact]
    public async Task PinnedRevision_IsNotResolvedAgain_WhenRecoveringWorkspace()
    {
            Fixture fixture = CreateFixture();

            Result<Job> created =
                    await fixture.Service.CreateAsync(
                            "request",
                            "KRX-DETERMINISTIC");

            await fixture.Orchestrator.AdvanceAsync(
                    created.Value.Id,
                    "orchestrator",
                    "corr-created");

            await fixture.Orchestrator.AdvanceAsync(
                    created.Value.Id,
                    "orchestrator",
                    "corr-context");

            string pinned =
                    created.Value.BaseRepositoryHead!;

            fixture.SourceRevision.Head =
                    "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

            await fixture.Orchestrator.AdvanceAsync(
                    created.Value.Id,
                    "orchestrator",
                    "corr-planning");

            await fixture.Orchestrator.AdvanceAsync(
                    created.Value.Id,
                    "orchestrator",
                    "corr-workspace");

            Assert.Equal(
                    pinned,
                    fixture.ExecutionPlane.LastExpectedHead);

            Assert.Equal(
                    pinned,
                    created.Value.BaseRepositoryHead);

            Assert.Equal(
                    1,
                    fixture.SourceRevision.CallCount);

            Assert.Equal(
                    1,
                    fixture.ExecutionPlane.PrepareCount);

            Assert.Equal(
                    1,
                    fixture.ExecutionPlane.RecoverCount);
    }


    [Fact]
    public async Task ContextBuilding_GeneratesContextAgainstPinnedWorktree()
    {
            Fixture fixture = CreateFixture();

            Result<Job> created =
                    await fixture.Service.CreateAsync(
                            "request",
                            "KRX-CONTEXT");

            await fixture.Orchestrator.AdvanceAsync(
                    created.Value.Id,
                    "orchestrator",
                    "corr-created");

            JobOperationResult result =
                    await fixture.Orchestrator.AdvanceAsync(
                            created.Value.Id,
                            "orchestrator",
                            "corr-context");

            Assert.True(result.IsSuccess);

            Assert.Equal(
                    JobState.Planning,
                    created.Value.State);

            Assert.Equal(
                    fixture.SourceRevision.Head,
                    created.Value.BaseRepositoryHead);

            Assert.Equal(
                    1,
                    fixture.ExecutionPlane.PrepareCount);

            Assert.Equal(
                    1,
                    fixture.Context.CallCount);

            ContextGenerationRequest request =
                    Assert.IsType<ContextGenerationRequest>(
                            fixture.Context.LastRequest);

            Guid expectedRunId =
                    new DeterministicJobRunIdProvider()
                            .Create(
                                    created.Value.Id,
                                    created.Value.AttemptCount);

            Assert.Equal(
                    expectedRunId,
                    request.RunId);

            Assert.Equal(
                    created.Value.Id,
                    request.JobId);

            Assert.Equal(
                    created.Value.ExternalId,
                    request.JobExternalId);

            Assert.Equal(
                    created.Value.BaseRepositoryHead,
                    request.BaseRepositoryHead);

            Assert.Equal(
                    created.Value.BaseRepositoryHead,
                    request.Repository.Head);

            Assert.Equal(
                    "corr-context",
                    request.CorrelationId);
    }

    [Fact]
    public async Task ContextBuilding_ExecutionFailure_PreservesPinnedRevision()
    {
            Fixture fixture = CreateFixture();

            Result<Job> created =
                    await fixture.Service.CreateAsync(
                            "request",
                            "KRX-CONTEXT-EXEC");

            await fixture.Orchestrator.AdvanceAsync(
                    created.Value.Id,
                    "orchestrator",
                    "corr-created");

            fixture.ExecutionPlane.FailPrepare = true;

            JobOperationResult result =
                    await fixture.Orchestrator.AdvanceAsync(
                            created.Value.Id,
                            "orchestrator",
                            "corr-context");

            Assert.False(result.IsSuccess);

            Assert.Equal(
                    JobOperationKind.RetryableFailure,
                    result.Kind);

            Assert.Equal(
                    JobApplicationErrors
                            .ExecutionPlanePreparationFailed,
                    result.Error);

            Assert.Equal(
                    JobState.ContextBuilding,
                    created.Value.State);

            Assert.Equal(
                    fixture.SourceRevision.Head,
                    created.Value.BaseRepositoryHead);

            Assert.Equal(
                    1,
                    fixture.ExecutionPlane.PrepareCount);

            Assert.Equal(
                    0,
                    fixture.Context.CallCount);
    }

    [Fact]
    public async Task ContextBuilding_RepositoryHeadMismatch_FailsClosed()
    {
            Fixture fixture = CreateFixture();

            Result<Job> created =
                    await fixture.Service.CreateAsync(
                            "request",
                            "KRX-CONTEXT-MISMATCH");

            await fixture.Orchestrator.AdvanceAsync(
                    created.Value.Id,
                    "orchestrator",
                    "corr-created");

            fixture.ExecutionPlane.PreparedHeadOverride =
                    "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

            JobOperationResult result =
                    await fixture.Orchestrator.AdvanceAsync(
                            created.Value.Id,
                            "orchestrator",
                            "corr-context");

            Assert.False(result.IsSuccess);

            Assert.Equal(
                    JobOperationKind.PermanentFailure,
                    result.Kind);

            Assert.Equal(
                    JobApplicationErrors
                            .ExecutionPlaneRepositoryMismatch,
                    result.Error);

            Assert.Equal(
                    JobState.ContextBuilding,
                    created.Value.State);

            Assert.Equal(
                    0,
                    fixture.Context.CallCount);
    }

    [Fact]
    public async Task ContextBuilding_SensitiveContentFailure_FailsClosed()
    {
            Fixture fixture = CreateFixture();

            Result<Job> created =
                    await fixture.Service.CreateAsync(
                            "request",
                            "KRX-CONTEXT-SENSITIVE");

            await fixture.Orchestrator.AdvanceAsync(
                    created.Value.Id,
                    "orchestrator",
                    "corr-created");

            fixture.Context.FailureKind =
                    ContextGenerationFailureKind
                            .SensitiveContentDetected;

            JobOperationResult result =
                    await fixture.Orchestrator.AdvanceAsync(
                            created.Value.Id,
                            "orchestrator",
                            "corr-context");

            Assert.False(result.IsSuccess);

            Assert.Equal(
                    JobOperationKind.PermanentFailure,
                    result.Kind);

            Assert.Equal(
                    JobApplicationErrors.ContextGenerationFailed,
                    result.Error);

            Assert.Equal(
                    JobState.ContextBuilding,
                    created.Value.State);

            Assert.Equal(
                    1,
                    fixture.Context.CallCount);
    }

	private sealed class FakeExecutionTargetProvider :
	        IExecutionTargetProvider
	{
	        public string DotnetTarget =>
	                "Kronxy.sln";
	}

	private sealed class FakeRestoreExecutionService :
	        IRestoreExecutionService
	{
	        public RestoreExecutionFailureKind FailureKind
	        {
	                get;
	                set;
	        }

	        public int CallCount
	        {
	                get;
	                private set;
	        }

	        public RestoreExecutionRequest? LastRequest
	        {
	                get;
	                private set;
	        }

	        public Task<RestoreExecutionResult> ExecuteAsync(
	                RestoreExecutionRequest request,
	                CancellationToken cancellationToken =
	                        default(CancellationToken))
	        {
	                cancellationToken
	                        .ThrowIfCancellationRequested();

	                CallCount++;
	                LastRequest = request;

	                if (FailureKind !=
	                    RestoreExecutionFailureKind.None)
	                {
	                        return Task.FromResult(
	                                RestoreExecutionResult.Failure(
	                                        FailureKind,
	                                        "TEST_RESTORE_FAILURE"));
	                }

	                DateTime started =
	                        new DateTime(
	                                2026,
	                                8,
	                                29,
	                                12,
	                                0,
	                                0,
	                                DateTimeKind.Utc);

	                RestoreExecutionReport report =
	                        new(
	                                request.JobId,
	                                request.RunId,
	                                request.Target,
	                                ToolExecutionOutcome.Completed,
	                                0,
	                                string.Empty,
	                                started,
	                                started.AddSeconds(1),
	                                TimeSpan.FromSeconds(1));

	                return Task.FromResult(
	                        RestoreExecutionResult.Success(
	                                report,
	                                Artifact(
	                                        request,
	                                        ArtifactType.RestoreReport,
	                                        "restore/report.json"),
	                                Artifact(
	                                        request,
	                                        ArtifactType.RestoreStandardOutput,
	                                        "restore/stdout.txt"),
	                                Artifact(
	                                        request,
	                                        ArtifactType.RestoreStandardError,
	                                        "restore/stderr.txt")));
	        }

	        private static ArtifactRecord Artifact(
	                RestoreExecutionRequest request,
	                ArtifactType artifactType,
	                string relativeName)
	        {
	                return new ArtifactRecord
	                {
	                        ArtifactId =
	                                Guid.NewGuid(),

	                        JobId =
	                                request.JobId,

	                        RunId =
	                                request.RunId,

	                        ArtifactType =
	                                artifactType,

	                        RelativePath =
	                                $"{request.JobId:N}/" +
	                                $"{request.RunId:N}/" +
	                                relativeName,

	                        Sha256 =
	                                new string(
	                                        'a',
	                                        64),

	                        SizeBytes =
	                                1,

	                        CreatedAtUtc =
	                                DateTimeOffset.UtcNow,

	                        CorrelationId =
	                                request.CorrelationId
	                };
	        }
	}

	[Fact]
	public async Task Developing_RestoreSuccess_AdvancesToBuilding()
	{
	        Fixture fixture =
	                CreateFixture();

	        Result<Job> created =
	                await fixture.Service.CreateAsync(
	                        "request",
	                        "KRX-RESTORE-OK");

	        await fixture.Orchestrator.AdvanceAsync(
	                created.Value.Id,
	                "orchestrator",
	                "corr-created");

	        await fixture.Orchestrator.AdvanceAsync(
	                created.Value.Id,
	                "orchestrator",
	                "corr-context");

	        await fixture.Orchestrator.AdvanceAsync(
	                created.Value.Id,
	                "orchestrator",
	                "corr-planning");

	        await fixture.Orchestrator.AdvanceAsync(
	                created.Value.Id,
	                "orchestrator",
	                "corr-workspace");

	        Assert.Equal(
	                JobState.Developing,
	                created.Value.State);

	        JobOperationResult result =
	                await fixture.Orchestrator.AdvanceAsync(
	                        created.Value.Id,
	                        "orchestrator",
	                        "corr-restore");

	        Assert.True(
	                result.IsSuccess);

	        Assert.Equal(
	                JobState.Building,
	                created.Value.State);

	        Assert.Equal(
	                1,
	                fixture.Restore.CallCount);

                Assert.Equal(1, fixture.Developer.CallCount);
                Assert.Equal(1, fixture.SafeChange.CallCount);

	        RestoreExecutionRequest request =
	                Assert.IsType<RestoreExecutionRequest>(
	                        fixture.Restore.LastRequest);

	        Assert.Equal(
	                created.Value.Id,
	                request.JobId);

	        Assert.Equal(
	                "Kronxy.sln",
	                request.Target);

	        Assert.Equal(
	                "corr-restore",
	                request.CorrelationId);

	        Assert.Equal(
	                created.Value.BaseRepositoryHead,
	                request.Repository.Head);

	        Assert.Equal(
	                2,
	                fixture.ExecutionPlane.RecoverCount);
	}

        [Fact]
        public async Task Developing_ValidDeveloperRecovery_SkipsInferenceAndRunsSafeChangeThenRestore()
        {
                Fixture fixture = CreateFixture();
                Result<Job> created = await fixture.Service.CreateAsync("request", "KRX-DEV-RECOVER");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "created");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "context");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "planning");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "workspace");
                var recovered = new ValidatedDeveloperProposal(
                        "Recovered",
                        new[] { new ValidatedDeveloperChange(DeveloperChangeOperationType.CreateFile, "src/recovered.cs", "Recover", "class Recovered {}", string.Empty, 18) },
                        Array.Empty<string>(), Array.Empty<string>(), 18, 100);
                fixture.RecoveryEvidence.Handler = request =>
                        request.Stage == RecoveryStage.Developer
                                ? StageRecoveryResult.Completed(recovered)
                                : StageRecoveryResult.NotCompleted();

                JobOperationResult result = await fixture.Orchestrator.AdvanceAsync(
                        created.Value.Id, "orchestrator", "recover");

                Assert.True(result.IsSuccess);
                Assert.Equal(JobState.Building, created.Value.State);
                Assert.Equal(0, fixture.Developer.CallCount);
                Assert.Equal(1, fixture.SafeChange.CallCount);
                Assert.Same(recovered, fixture.SafeChange.LastRequest!.Proposal);
                Assert.Equal(1, fixture.Restore.CallCount);
        }

        [Fact]
        public async Task Developing_InvalidDeveloperEvidence_FailsBeforeExternalExecution()
        {
                Fixture fixture = CreateFixture();
                Result<Job> created = await fixture.Service.CreateAsync("request", "KRX-DEV-EVIDENCE-BAD");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "created");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "context");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "planning");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "workspace");
                fixture.RecoveryEvidence.Handler = request =>
                        request.Stage == RecoveryStage.Developer
                                ? StageRecoveryResult.InvalidEvidence("TAMPERED")
                                : StageRecoveryResult.NotCompleted();

                JobOperationResult result = await fixture.Orchestrator.AdvanceAsync(
                        created.Value.Id, "orchestrator", "recover-bad");

                Assert.False(result.IsSuccess);
                Assert.Equal(JobState.Developing, created.Value.State);
                Assert.Equal(0, fixture.Developer.CallCount);
                Assert.Equal(0, fixture.SafeChange.CallCount);
                Assert.Equal(0, fixture.Restore.CallCount);
        }

        [Fact]
        public async Task Developing_DeveloperFailure_DoesNotApplyOrRestore()
        {
                Fixture fixture = CreateFixture();
                Result<Job> created = await fixture.Service.CreateAsync("request", "KRX-DEVELOPER-FAIL");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "corr-created");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "corr-context");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "corr-planning");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "corr-workspace");
                fixture.Developer.FailureKind = DeveloperExecutionFailureKind.AiInvalidResponse;

                JobOperationResult result = await fixture.Orchestrator.AdvanceAsync(
                        created.Value.Id, "orchestrator", "corr-developer-fail");

                Assert.False(result.IsSuccess);
                Assert.Equal(JobApplicationErrors.DeveloperExecutionFailed, result.Error);
                Assert.Equal(JobState.Developing, created.Value.State);
                Assert.Equal(1, fixture.Developer.CallCount);
                Assert.Equal(0, fixture.SafeChange.CallCount);
                Assert.Equal(0, fixture.Restore.CallCount);
        }

        [Fact]
        public async Task Developing_SafeChangeFailure_DoesNotRestore()
        {
                Fixture fixture = CreateFixture();
                Result<Job> created = await fixture.Service.CreateAsync("request", "KRX-SAFE-FAIL");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "corr-created");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "corr-context");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "corr-planning");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "corr-workspace");
                fixture.SafeChange.FailureKind = SafeChangeApplicationFailureKind.PreconditionFailed;

                JobOperationResult result = await fixture.Orchestrator.AdvanceAsync(
                        created.Value.Id, "orchestrator", "corr-safe-fail");

                Assert.False(result.IsSuccess);
                Assert.Equal(JobApplicationErrors.SafeChangeApplicationFailed, result.Error);
                Assert.Equal(JobState.Developing, created.Value.State);
                Assert.Equal(1, fixture.Developer.CallCount);
                Assert.Equal(1, fixture.SafeChange.CallCount);
                Assert.Equal(0, fixture.Restore.CallCount);
                Assert.NotNull(fixture.SafeChange.LastRequest?.Proposal);
        }

        [Fact]
        public async Task Developing_ObservedChangeFailure_UsesObservedSpecificError()
        {
                Fixture fixture = CreateFixture();
                Result<Job> created = await fixture.Service.CreateAsync(
                        "request", "KRX-OBSERVED-FAIL");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "created");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "context");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "planning");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "workspace");
                fixture.Observed.FailureKind =
                        ObservedChangeEvidenceFailureKind.UnsafePath;

                JobOperationResult result = await fixture.Orchestrator.AdvanceAsync(
                        created.Value.Id, "orchestrator", "observed-fail");

                Assert.False(result.IsSuccess);
                Assert.Equal(JobState.Developing, created.Value.State);
                Assert.Equal(JobApplicationErrors.ObservedChangeEvidenceInvalid, result.Error);
                Assert.NotEqual(JobApplicationErrors.ReviewerEvidenceInvalid, result.Error);
                Assert.Equal(1, fixture.Observed.CallCount);
                Assert.Equal(0, fixture.Restore.CallCount);
                Assert.Equal(0, fixture.Reviewer.CallCount);
        }

	[Fact]
	public async Task Developing_RestoreFailure_DoesNotAdvance()
	{
	        Fixture fixture =
	                CreateFixture();

	        Result<Job> created =
	                await fixture.Service.CreateAsync(
	                        "request",
	                        "KRX-RESTORE-FAIL");

	        await fixture.Orchestrator.AdvanceAsync(
	                created.Value.Id,
	                "orchestrator",
	                "corr-created");

	        await fixture.Orchestrator.AdvanceAsync(
	                created.Value.Id,
	                "orchestrator",
	                "corr-context");

	        await fixture.Orchestrator.AdvanceAsync(
	                created.Value.Id,
	                "orchestrator",
	                "corr-planning");

	        await fixture.Orchestrator.AdvanceAsync(
	                created.Value.Id,
	                "orchestrator",
	                "corr-workspace");

	        fixture.Restore.FailureKind =
	                RestoreExecutionFailureKind
	                        .RestoreFailed;

	        int saveCount =
	                fixture.UnitOfWork.SaveCount;

	        JobOperationResult result =
	                await fixture.Orchestrator.AdvanceAsync(
	                        created.Value.Id,
	                        "orchestrator",
	                        "corr-restore-fail");

	        Assert.False(
	                result.IsSuccess);

	        Assert.Equal(
	                JobOperationKind.RetryableFailure,
	                result.Kind);

	        Assert.Equal(
	                JobApplicationErrors.RestoreExecutionFailed,
	                result.Error);

	        Assert.Equal(
	                JobState.Developing,
	                created.Value.State);

	        Assert.Equal(
	                saveCount + 2,
	                fixture.UnitOfWork.SaveCount);

	        Assert.Equal(
	                1,
	                fixture.Restore.CallCount);
	}

	[Fact]
	public async Task Building_BuildSuccess_AdvancesToTesting()
	{
	        Fixture fixture =
	                CreateFixture();

	        Result<Job> created =
	                await fixture.Service.CreateAsync(
	                        "request",
	                        "KRX-BUILD-OK");

	        await fixture.Orchestrator.AdvanceAsync(
	                created.Value.Id,
	                "orchestrator",
	                "corr-created");

	        await fixture.Orchestrator.AdvanceAsync(
	                created.Value.Id,
	                "orchestrator",
	                "corr-context");

	        await fixture.Orchestrator.AdvanceAsync(
	                created.Value.Id,
	                "orchestrator",
	                "corr-planning");

	        await fixture.Orchestrator.AdvanceAsync(
	                created.Value.Id,
	                "orchestrator",
	                "corr-workspace");

	        await fixture.Orchestrator.AdvanceAsync(
	                created.Value.Id,
	                "orchestrator",
	                "corr-restore");

	        Assert.Equal(
	                JobState.Building,
	                created.Value.State);

	        JobOperationResult result =
	                await fixture.Orchestrator.AdvanceAsync(
	                        created.Value.Id,
	                        "orchestrator",
	                        "corr-build");

	        Assert.True(
	                result.IsSuccess);

	        Assert.Equal(
	                JobState.Testing,
	                created.Value.State);

	        Assert.Equal(
	                1,
	                fixture.Build.CallCount);

	        BuildExecutionRequest request =
	                Assert.IsType<BuildExecutionRequest>(
	                        fixture.Build.LastRequest);

	        Assert.Equal(
	                created.Value.Id,
	                request.JobId);

	        Assert.Equal(
	                "Kronxy.sln",
	                request.Target);

	        Assert.Equal(
	                "corr-build",
	                request.CorrelationId);

	        Assert.Equal(
	                created.Value.BaseRepositoryHead,
	                request.Repository.Head);

	        Assert.Equal(
	                3,
	                fixture.ExecutionPlane.RecoverCount);
	}

	[Fact]
	public async Task Building_BuildFailure_DoesNotAdvance()
	{
	        Fixture fixture =
	                CreateFixture();

	        Result<Job> created =
	                await fixture.Service.CreateAsync(
	                        "request",
	                        "KRX-BUILD-FAIL");

	        await fixture.Orchestrator.AdvanceAsync(
	                created.Value.Id,
	                "orchestrator",
	                "corr-created");

	        await fixture.Orchestrator.AdvanceAsync(
	                created.Value.Id,
	                "orchestrator",
	                "corr-context");

	        await fixture.Orchestrator.AdvanceAsync(
	                created.Value.Id,
	                "orchestrator",
	                "corr-planning");

	        await fixture.Orchestrator.AdvanceAsync(
	                created.Value.Id,
	                "orchestrator",
	                "corr-workspace");

	        await fixture.Orchestrator.AdvanceAsync(
	                created.Value.Id,
	                "orchestrator",
	                "corr-restore");

	        fixture.Build.FailureKind =
	                BuildExecutionFailureKind.BuildFailed;

	        int saveCount =
	                fixture.UnitOfWork.SaveCount;

	        JobOperationResult result =
	                await fixture.Orchestrator.AdvanceAsync(
	                        created.Value.Id,
	                        "orchestrator",
	                        "corr-build-fail");

	        Assert.False(
	                result.IsSuccess);

	        Assert.Equal(
	                JobOperationKind.RetryableFailure,
	                result.Kind);

	        Assert.Equal(
	                JobApplicationErrors.BuildExecutionFailed,
	                result.Error);

	        Assert.Equal(
	                JobState.Building,
	                created.Value.State);

	        Assert.Equal(
	                saveCount + 2,
	                fixture.UnitOfWork.SaveCount);

	        Assert.Equal(
	                1,
	                fixture.Build.CallCount);
	}

   [Fact]
   public async Task Testing_TestSuccess_AdvancesToReviewing()
   {
           Fixture fixture =
                   CreateFixture();

           Result<Job> created =
                   await fixture.Service.CreateAsync(
                           "request",
                           "KRX-TEST-OK");

           await fixture.Orchestrator.AdvanceAsync(
                   created.Value.Id,
                   "orchestrator",
                   "corr-created");

           await fixture.Orchestrator.AdvanceAsync(
                   created.Value.Id,
                   "orchestrator",
                   "corr-context");

           await fixture.Orchestrator.AdvanceAsync(
                   created.Value.Id,
                   "orchestrator",
                   "corr-planning");

           await fixture.Orchestrator.AdvanceAsync(
                   created.Value.Id,
                   "orchestrator",
                   "corr-workspace");

           await fixture.Orchestrator.AdvanceAsync(
                   created.Value.Id,
                   "orchestrator",
                   "corr-restore");

           await fixture.Orchestrator.AdvanceAsync(
                   created.Value.Id,
                   "orchestrator",
                   "corr-build");

           Assert.Equal(
                   JobState.Testing,
                   created.Value.State);

           JobOperationResult result =
                   await fixture.Orchestrator.AdvanceAsync(
                           created.Value.Id,
                           "orchestrator",
                           "corr-test");

           Assert.True(
                   result.IsSuccess);

           Assert.Equal(
                   JobState.Reviewing,
                   created.Value.State);

           Assert.Equal(
                   1,
                   fixture.Test.CallCount);

           TestExecutionRequest request =
                   Assert.IsType<TestExecutionRequest>(
                           fixture.Test.LastRequest);

           Assert.Equal(
                   created.Value.Id,
                   request.JobId);

           Assert.Equal(
                   "Kronxy.sln",
                   request.Target);

           Assert.Equal(
                   "corr-test",
                   request.CorrelationId);

           Assert.Equal(
                   created.Value.BaseRepositoryHead,
                   request.Repository.Head);

           Assert.Equal(
                   4,
                   fixture.ExecutionPlane.RecoverCount);
   }

   [Fact]
   public async Task Testing_TestFailure_DoesNotAdvance()
   {
           Fixture fixture =
                   CreateFixture();

           Result<Job> created =
                   await fixture.Service.CreateAsync(
                           "request",
                           "KRX-TEST-FAIL");

           await fixture.Orchestrator.AdvanceAsync(
                   created.Value.Id,
                   "orchestrator",
                   "corr-created");

           await fixture.Orchestrator.AdvanceAsync(
                   created.Value.Id,
                   "orchestrator",
                   "corr-context");

           await fixture.Orchestrator.AdvanceAsync(
                   created.Value.Id,
                   "orchestrator",
                   "corr-planning");

           await fixture.Orchestrator.AdvanceAsync(
                   created.Value.Id,
                   "orchestrator",
                   "corr-workspace");

           await fixture.Orchestrator.AdvanceAsync(
                   created.Value.Id,
                   "orchestrator",
                   "corr-restore");

           await fixture.Orchestrator.AdvanceAsync(
                   created.Value.Id,
                   "orchestrator",
                   "corr-build");

           Assert.Equal(
                   JobState.Testing,
                   created.Value.State);

           fixture.Test.FailureKind =
                   TestExecutionFailureKind.TestsFailed;

           int saveCount =
                   fixture.UnitOfWork.SaveCount;

           JobOperationResult result =
                   await fixture.Orchestrator.AdvanceAsync(
                           created.Value.Id,
                           "orchestrator",
                           "corr-test-fail");

           Assert.False(
                   result.IsSuccess);

           Assert.Equal(
                   JobOperationKind.RetryableFailure,
                   result.Kind);

           Assert.Equal(
                   JobApplicationErrors.TestExecutionFailed,
                   result.Error);

           Assert.Equal(
                   JobState.Testing,
                   created.Value.State);

           Assert.Equal(
                   saveCount + 2,
                   fixture.UnitOfWork.SaveCount);

           Assert.Equal(
                   1,
                   fixture.Test.CallCount);

           TestExecutionRequest request =
                   Assert.IsType<TestExecutionRequest>(
                           fixture.Test.LastRequest);

           Assert.Equal(
                   "corr-test-fail",
                   request.CorrelationId);

           Assert.Equal(
                   created.Value.BaseRepositoryHead,
                   request.Repository.Head);
   }

	private sealed class FakeBuildExecutionService :
	        IBuildExecutionService
	{
	        public BuildExecutionFailureKind FailureKind
	        {
	                get;
	                set;
	        }

	        public int CallCount
	        {
	                get;
	                private set;
	        }

	        public BuildExecutionRequest? LastRequest
	        {
	                get;
	                private set;
	        }

	        public Task<BuildExecutionResult> ExecuteAsync(
	                BuildExecutionRequest request,
	                CancellationToken cancellationToken =
	                        default(CancellationToken))
	        {
	                cancellationToken
	                        .ThrowIfCancellationRequested();

	                CallCount++;
	                LastRequest = request;

	                if (FailureKind !=
	                    BuildExecutionFailureKind.None)
	                {
	                        return Task.FromResult(
	                                BuildExecutionResult.Failure(
	                                        FailureKind,
	                                        "TEST_BUILD_FAILURE"));
	                }

	                DateTime started =
	                        new DateTime(
	                                2026, 8, 29,
	                                12, 0, 0,
	                                DateTimeKind.Utc);

	                BuildExecutionReport report =
	                        new(
	                                request.JobId,
	                                request.RunId,
	                                request.Target,
	                                ToolExecutionOutcome.Completed,
	                                0,
	                                string.Empty,
	                                started,
	                                started.AddSeconds(1),
	                                TimeSpan.FromSeconds(1));

	                return Task.FromResult(
	                        BuildExecutionResult.Success(
	                                report,
	                                Artifact(
	                                        request,
	                                        ArtifactType.BuildReport,
	                                        "build/report.json"),
	                                Artifact(
	                                        request,
	                                        ArtifactType.BuildStandardOutput,
	                                        "build/stdout.txt"),
	                                Artifact(
	                                        request,
	                                        ArtifactType.BuildStandardError,
	                                        "build/stderr.txt")));
	        }

	        private static ArtifactRecord Artifact(
	                BuildExecutionRequest request,
	                ArtifactType artifactType,
	                string relativeName)
	        {
	                return new ArtifactRecord
	                {
	                        ArtifactId = Guid.NewGuid(),
	                        JobId = request.JobId,
	                        RunId = request.RunId,
	                        ArtifactType = artifactType,
	                        RelativePath =
	                                $"{request.JobId:N}/" +
	                                $"{request.RunId:N}/" +
	                                relativeName,
	                        Sha256 = new string('b', 64),
	                        SizeBytes = 1,
	                        CreatedAtUtc = DateTimeOffset.UtcNow,
	                        CorrelationId = request.CorrelationId
	                };
	        }
	}

   private sealed class FakeTestExecutionService :
           ITestExecutionService
   {
           public TestExecutionFailureKind FailureKind
           {
                   get;
                   set;
           }

           public int CallCount
           {
                   get;
                   private set;
           }

           public TestExecutionRequest? LastRequest
           {
                   get;
                   private set;
           }

           public Task<TestExecutionResult> ExecuteAsync(
                   TestExecutionRequest request,
                   CancellationToken cancellationToken =
                           default(CancellationToken))
           {
                   cancellationToken
                           .ThrowIfCancellationRequested();

                   CallCount++;
                   LastRequest = request;

                   if (FailureKind !=
                       TestExecutionFailureKind.None)
                   {
                           return Task.FromResult(
                                   TestExecutionResult.Failure(
                                           FailureKind,
                                           "TEST_TEST_FAILURE"));
                   }

                   DateTime started =
                           new DateTime(
                                   2026, 8, 29,
                                   12, 0, 0,
                                   DateTimeKind.Utc);

                   TestExecutionReport report =
                           new(
                                   request.JobId,
                                   request.RunId,
                                   request.Target,
                                   ToolExecutionOutcome.Completed,
                                   0,
                                   string.Empty,
                                   started,
                                   started.AddSeconds(1),
                                   TimeSpan.FromSeconds(1));

                   return Task.FromResult(
                           TestExecutionResult.Success(
                                   report,
                                   Artifact(
                                           request,
                                           ArtifactType.TestReport,
                                           "tests/report.json"),
                                   Artifact(
                                           request,
                                           ArtifactType.TestResults,
                                           "tests/results.zip"),
                                   Artifact(
                                           request,
                                           ArtifactType.TestStandardOutput,
                                           "tests/stdout.txt"),
                                   Artifact(
                                           request,
                                           ArtifactType.TestStandardError,
                                           "tests/stderr.txt")));
           }

           private static ArtifactRecord Artifact(
                   TestExecutionRequest request,
                   ArtifactType artifactType,
                   string relativeName)
           {
                   return new ArtifactRecord
                   {
                           ArtifactId =
                                   Guid.NewGuid(),

                           JobId =
                                   request.JobId,

                           RunId =
                                   request.RunId,

                           ArtifactType =
                                   artifactType,

                           RelativePath =
                                   $"{request.JobId:N}/" +
                                   $"{request.RunId:N}/" +
                                   relativeName,

                           Sha256 =
                                   new string('c', 64),

                           SizeBytes =
                                   1,

                           CreatedAtUtc =
                                   DateTimeOffset.UtcNow,

                           CorrelationId =
                                   request.CorrelationId
                   };
           }
   }

        [Theory]
        [InlineData(
                RecoveryStage.Context,
                JobState.ContextBuilding,
                JobState.Planning)]
        [InlineData(
                RecoveryStage.Planning,
                JobState.Planning,
                JobState.WorkspacePreparing)]
        [InlineData(
                RecoveryStage.Restore,
                JobState.Developing,
                JobState.Building)]
        [InlineData(
                RecoveryStage.Build,
                JobState.Building,
                JobState.Testing)]
        [InlineData(
                RecoveryStage.Test,
                JobState.Testing,
                JobState.Reviewing)]
        public async Task Recovered_stage_evidence_skips_external_execution(
                RecoveryStage recoveredStage,
                JobState stageState,
                JobState expectedState)
        {
                Fixture fixture =
                        CreateFixture();

                fixture.RecoveryEvidence.Handler =
                        request =>
                                request.Stage == recoveredStage
                                        ? StageRecoveryResult.Completed()
                                        : StageRecoveryResult.NotCompleted();

                Result<Job> created =
                        await fixture.Service.CreateAsync(
                                "request",
                                $"KRX-RECOVER-{recoveredStage}");

                int guard = 0;

                while (created.Value.State != stageState &&
                       guard < 10)
                {
                        JobOperationResult advance =
                                await fixture.Orchestrator.AdvanceAsync(
                                        created.Value.Id,
                                        "orchestrator",
                                        $"corr-pre-{guard}");

                        Assert.True(
                                advance.IsSuccess);

                        guard++;
                }

                Assert.Equal(
                        stageState,
                        created.Value.State);

                int contextBefore =
                        fixture.Context.CallCount;

                int planningBefore =
                        fixture.Planning.CallCount;

                int restoreBefore =
                        fixture.Restore.CallCount;

                int buildBefore =
                        fixture.Build.CallCount;

                int testBefore =
                        fixture.Test.CallCount;

                JobOperationResult result =
                        await fixture.Orchestrator.AdvanceAsync(
                                created.Value.Id,
                                "orchestrator",
                                "corr-recovered-stage");

                Assert.True(
                        result.IsSuccess);

                Assert.Equal(
                        expectedState,
                        created.Value.State);

                StageRecoveryRequest recoveryRequest =
                        fixture.RecoveryEvidence.Requests.Last();

                Assert.Equal(
                        recoveredStage,
                        recoveryRequest.Stage);

                switch (recoveredStage)
                {
                        case RecoveryStage.Context:
                                Assert.Equal(
                                        contextBefore,
                                        fixture.Context.CallCount);
                                break;

                        case RecoveryStage.Planning:
                                Assert.Equal(
                                        planningBefore,
                                        fixture.Planning.CallCount);
                                break;

                        case RecoveryStage.Restore:
                                Assert.Equal(
                                        restoreBefore,
                                        fixture.Restore.CallCount);
                                break;

                        case RecoveryStage.Build:
                                Assert.Equal(
                                        buildBefore,
                                        fixture.Build.CallCount);
                                break;

                        case RecoveryStage.Test:
                                Assert.Equal(
                                        testBefore,
                                        fixture.Test.CallCount);
                                break;

                        default:
                                throw new InvalidOperationException(
                                        $"Unexpected recovery stage: {recoveredStage}");
                }
        }

        [Theory]
        [InlineData(
                StageRecoveryStatus.InvalidEvidence,
                JobOperationKind.PermanentFailure)]
        [InlineData(
                StageRecoveryStatus.Cancelled,
                JobOperationKind.Cancelled)]
        [InlineData(
                StageRecoveryStatus.Failure,
                JobOperationKind.RetryableFailure)]
        public async Task Invalid_recovery_evidence_fails_closed_without_external_execution(
                StageRecoveryStatus recoveryStatus,
                JobOperationKind expectedKind)
        {
                Fixture fixture =
                        CreateFixture();

                Result<Job> created =
                        await fixture.Service.CreateAsync(
                                "request",
                                $"KRX-RECOVERY-FAIL-{recoveryStatus}");

                await fixture.Orchestrator.AdvanceAsync(
                        created.Value.Id,
                        "orchestrator",
                        "corr-created");

                await fixture.Orchestrator.AdvanceAsync(
                        created.Value.Id,
                        "orchestrator",
                        "corr-context");

                await fixture.Orchestrator.AdvanceAsync(
                        created.Value.Id,
                        "orchestrator",
                        "corr-planning");

                await fixture.Orchestrator.AdvanceAsync(
                        created.Value.Id,
                        "orchestrator",
                        "corr-workspace");

                Assert.Equal(
                        JobState.Developing,
                        created.Value.State);

                int restoreBefore =
                        fixture.Restore.CallCount;

                fixture.RecoveryEvidence.Handler =
                        request =>
                        {
                                if (request.Stage !=
                                    RecoveryStage.Restore)
                                {
                                        return StageRecoveryResult
                                                .NotCompleted();
                                }

                                return recoveryStatus switch
                                {
                                        StageRecoveryStatus
                                                .InvalidEvidence =>
                                                StageRecoveryResult
                                                        .InvalidEvidence(
                                                                "BAD_EVIDENCE"),

                                        StageRecoveryStatus
                                                .Cancelled =>
                                                StageRecoveryResult
                                                        .Cancelled(
                                                                "RECOVERY_CANCELLED"),

                                        StageRecoveryStatus
                                                .Failure =>
                                                StageRecoveryResult
                                                        .Failure(
                                                                "RECOVERY_FAILED"),

                                        _ =>
                                                throw new
                                                        InvalidOperationException()
                                };
                        };

                JobOperationResult result =
                        await fixture.Orchestrator.AdvanceAsync(
                                created.Value.Id,
                                "orchestrator",
                                "corr-recovery-failure");

                Assert.False(
                        result.IsSuccess);

                Assert.Equal(
                        expectedKind,
                        result.Kind);

                Assert.Equal(
                        JobState.Developing,
                        created.Value.State);

                Assert.Equal(
                        restoreBefore,
                        fixture.Restore.CallCount);
        }

        [Fact]
        public async Task ChangesRequired_starts_next_bounded_attempt_with_distinct_run_identity()
        {
                Fixture fixture = CreateFixture(maxAttempts: 3);
                Result<Job> created = await fixture.Service.CreateAsync("request", "KRX-CORRECTION-001");
                int guard=0;
                while (created.Value.State != JobState.Reviewing && guard++ < 10)
                        Assert.True((await fixture.Orchestrator.AdvanceAsync(created.Value.Id,"orchestrator","corr")).IsSuccess);

                fixture.Reviewer.Review = new ReviewerReview
                { Decision=ReviewerDecision.ChangesRequired, Findings=[], RequiredCorrections=[
                    new ReviewerCorrection { RelativePath="src/generated.cs", Instruction="correct it" }],
                  RiskAssessment="medium", Summary="changes" };
                fixture.RecoveryEvidence.Handler = request => ReviewerEvidence(request, created.Value.BaseRepositoryHead!);

                Assert.True((await fixture.Orchestrator.AdvanceAsync(created.Value.Id,"orchestrator","review")).IsSuccess);
                Assert.Equal(JobState.Developing, created.Value.State);
                Assert.Equal(2, created.Value.AttemptCount);
                var provider=new DeterministicJobRunIdProvider();
                Guid firstRun = provider.Create(created.Value.Id,1);
                Assert.NotEqual(firstRun,provider.Create(created.Value.Id,2));

                ReviewerReview feedback = fixture.Reviewer.Review;
                fixture.RecoveryEvidence.Handler = request => request.Stage == RecoveryStage.Reviewer
                        ? StageRecoveryResult.Completed(reviewerReview: feedback)
                        : request.Stage == RecoveryStage.Developer && request.RunId != firstRun
                                ? StageRecoveryResult.NotCompleted()
                                : ReviewerEvidence(request, created.Value.BaseRepositoryHead!);
                Assert.True((await fixture.Orchestrator.AdvanceAsync(created.Value.Id,"orchestrator","attempt-2")).IsSuccess);
                Assert.Same(feedback, fixture.Developer.LastRequest!.ReviewerFeedback);
                Assert.Equal(firstRun, fixture.Developer.LastRequest.AuthorizedEvidenceRunId);
        }

        [Fact]
        public async Task ChangesRequired_at_max_attempts_stops_at_human_boundary()
        {
                Fixture fixture = CreateFixture(maxAttempts: 1);
                Result<Job> created = await fixture.Service.CreateAsync("request", "KRX-CORRECTION-MAX");
                int guard=0;
                while (created.Value.State != JobState.Reviewing && guard++ < 10)
                        Assert.True((await fixture.Orchestrator.AdvanceAsync(created.Value.Id,"orchestrator","corr")).IsSuccess);
                fixture.Reviewer.Review = new ReviewerReview
                { Decision=ReviewerDecision.ChangesRequired, Findings=[], RequiredCorrections=[],
                  RiskAssessment="high", Summary="changes" };
                fixture.RecoveryEvidence.Handler = request => ReviewerEvidence(request, created.Value.BaseRepositoryHead!);

                Assert.True((await fixture.Orchestrator.AdvanceAsync(created.Value.Id,"orchestrator","review")).IsSuccess);
                Assert.Equal(JobState.WaitingHuman,created.Value.State);
                Assert.Equal(1,created.Value.AttemptCount);
        }

        [Fact]
        public async Task Human_review_changes_required_runs_governed_same_attempt_cycle()
        {
                Fixture fixture = CreateFixture(maxAttempts: 3);
                Result<Job> created = await fixture.Service.CreateAsync("request", "KRX-HUMAN-CORRECTION");
                fixture.RecoveryEvidence.Handler = request => ReviewerEvidence(
                        request, created.Value.BaseRepositoryHead ?? fixture.SourceRevision.Head);

                int guard = 0;
                while (created.Value.State != JobState.WaitingHuman && guard++ < 12)
                        Assert.True((await fixture.Orchestrator.AdvanceAsync(
                                created.Value.Id, "orchestrator", "initial")).IsSuccess);

                int attemptBefore = created.Value.AttemptCount;
                Guid runId = new DeterministicJobRunIdProvider().Create(created.Value.Id, attemptBefore);
                var humanEvidence = new HumanReviewCorrectionEvidence
                {
                        JobId = created.Value.Id,
                        RunId = runId,
                        Decision = HumanReviewDecision.ChangesRequired,
                        RequiredCorrections =
                        [
                                new HumanReviewRequiredCorrection
                                {
                                        RelativePath = "src/generated.cs",
                                        Instruction = "Replace the existing file to satisfy the human finding."
                                }
                        ],
                        RecordedAtUtc = DateTimeOffset.UtcNow
                };

                fixture.RecoveryEvidence.Handler = request => request.Stage switch
                {
                        RecoveryStage.HumanReviewCorrection => StageRecoveryResult.NotCompleted(),
                        RecoveryStage.ReviewerOriginal => StageRecoveryResult.Completed(
                                reviewerReview: new ReviewerReview
                                {
                                        Decision = ReviewerDecision.Approved,
                                        Findings = [], RequiredCorrections = [],
                                        RiskAssessment = "low", Summary = "approved"
                                }),
                        _ => ReviewerEvidence(request, created.Value.BaseRepositoryHead!)
                };

                JobOperationResult requested = await fixture.Orchestrator.RequestHumanReviewCorrectionAsync(
                        created.Value.Id,
                        new HumanReviewCorrectionRequest
                        {
                                Actor = "human-reviewer",
                                CorrelationId = "human-correction",
                                RequiredCorrections = humanEvidence.RequiredCorrections
                        });

                Assert.True(requested.IsSuccess);
                Assert.Equal(JobState.Developing, created.Value.State);
                Assert.Equal(attemptBefore, created.Value.AttemptCount);
                Assert.Single(fixture.Artifacts.Writes);
                Assert.Equal(ArtifactType.HumanReviewCorrectionEvidence,
                        fixture.Artifacts.Writes[0].ArtifactType);

                fixture.RecoveryEvidence.Handler = request => request.Stage switch
                {
                        RecoveryStage.HumanReviewCorrection => StageRecoveryResult.Completed(
                                humanReviewCorrection: humanEvidence),
                        RecoveryStage.EffectiveDeveloperProposal =>
                                StageRecoveryResult.Completed(
                                        developerProposal: ReviewerEvidence(
                                                request,
                                                created.Value.BaseRepositoryHead!)
                                            .DeveloperProposal,
                                        developerProposalLineage:
                                            DeveloperProposalLineage.HumanReviewCorrection),
                        RecoveryStage.DeveloperHumanReviewCorrection or
                        RecoveryStage.ReviewerHumanReviewCorrection => StageRecoveryResult.NotCompleted(),
                        RecoveryStage.ObservedHumanReviewCorrection =>
                                created.Value.State == JobState.Reviewing
                                        ? StageRecoveryResult.Completed(observedChangeManifest:
                                                new ObservedChangeManifest(
                                                        created.Value.Id, runId,
                                                        created.Value.BaseRepositoryHead!, []))
                                        : StageRecoveryResult.NotCompleted(),
                        RecoveryStage.BuildHumanReviewCorrection =>
                                created.Value.State == JobState.Reviewing
                                        ? StageRecoveryResult.Completed(buildReport:
                                                new BuildExecutionReport(
                                                        created.Value.Id, runId, "target",
                                                        ToolExecutionOutcome.Completed, 0, "",
                                                        DateTime.UtcNow, DateTime.UtcNow, TimeSpan.Zero))
                                        : StageRecoveryResult.NotCompleted(),
                        RecoveryStage.TestHumanReviewCorrection =>
                                created.Value.State == JobState.Reviewing
                                        ? StageRecoveryResult.Completed(testReport:
                                                new TestExecutionReport(
                                                        created.Value.Id, runId, "target",
                                                        ToolExecutionOutcome.Completed, 0, "",
                                                        DateTime.UtcNow, DateTime.UtcNow, TimeSpan.Zero))
                                        : StageRecoveryResult.NotCompleted(),
                        RecoveryStage.ReviewerOriginal => StageRecoveryResult.Completed(
                                reviewerReview: new ReviewerReview
                                {
                                        Decision = ReviewerDecision.Approved,
                                        Findings = [], RequiredCorrections = [],
                                        RiskAssessment = "low", Summary = "approved"
                                }),
                        _ => ReviewerEvidence(request, created.Value.BaseRepositoryHead!)
                };

                Assert.True((await fixture.Orchestrator.AdvanceAsync(
                        created.Value.Id, "orchestrator", "human-developer")).IsSuccess);
                Assert.Equal(JobState.Building, created.Value.State);
                Assert.NotNull(fixture.Developer.LastRequest?.HumanReviewCorrection);
                Assert.True(fixture.SafeChange.LastRequest?.IsHumanReviewCorrection);
                Assert.True(fixture.Observed.LastRequest?.IsHumanReviewCorrection);

                Assert.True((await fixture.Orchestrator.AdvanceAsync(
                        created.Value.Id, "orchestrator", "human-build")).IsSuccess);
                Assert.Equal(JobState.Testing, created.Value.State);
                Assert.True(fixture.Build.LastRequest?.IsHumanReviewCorrection);

                Assert.True((await fixture.Orchestrator.AdvanceAsync(
                        created.Value.Id, "orchestrator", "human-test")).IsSuccess);
                Assert.Equal(JobState.Reviewing, created.Value.State);
                Assert.True(fixture.Test.LastRequest?.IsHumanReviewCorrection);

                Assert.True((await fixture.Orchestrator.AdvanceAsync(
                        created.Value.Id, "orchestrator", "human-review")).IsSuccess);
                Assert.Equal(JobState.WaitingHuman, created.Value.State);
                Assert.Equal(attemptBefore, created.Value.AttemptCount);
                Assert.NotNull(fixture.Reviewer.LastRequest?.HumanReviewCorrection);
        }

        [Fact]
        public async Task Human_review_correction_rejects_path_outside_original_plan()
        {
                Fixture fixture = CreateFixture();
                Result<Job> created = await fixture.Service.CreateAsync("request", "KRX-HUMAN-PATH");
                fixture.RecoveryEvidence.Handler = request => ReviewerEvidence(
                        request, created.Value.BaseRepositoryHead ?? fixture.SourceRevision.Head);
                int guard = 0;
                while (created.Value.State != JobState.WaitingHuman && guard++ < 12)
                        Assert.True((await fixture.Orchestrator.AdvanceAsync(
                                created.Value.Id, "orchestrator", "initial")).IsSuccess);

                fixture.RecoveryEvidence.Handler = request => request.Stage switch
                {
                        RecoveryStage.HumanReviewCorrection => StageRecoveryResult.NotCompleted(),
                        RecoveryStage.ReviewerOriginal => StageRecoveryResult.Completed(
                                reviewerReview: fixture.Reviewer.Review),
                        _ => ReviewerEvidence(request, created.Value.BaseRepositoryHead!)
                };
                JobOperationResult result = await fixture.Orchestrator.RequestHumanReviewCorrectionAsync(
                        created.Value.Id,
                        new HumanReviewCorrectionRequest
                        {
                                Actor = "human", CorrelationId = "outside",
                                RequiredCorrections =
                                [
                                        new HumanReviewRequiredCorrection
                                        {
                                                RelativePath = "src/outside.cs",
                                                Instruction = "unauthorized"
                                        }
                                ]
                        });

                Assert.False(result.IsSuccess);
                Assert.Equal(JobState.WaitingHuman, created.Value.State);
                Assert.Empty(fixture.Artifacts.Writes);
        }

        [Fact]
        public async Task Human_review_approval_completes_with_audited_evidence_only()
        {
                (Fixture fixture, Job job) = await WaitingHumanJob("KRX-APPROVE");
                ConfigureApprovalEvidence(fixture, job);
                int developer = fixture.Developer.CallCount;
                int safeChange = fixture.SafeChange.CallCount;
                int build = fixture.Build.CallCount;
                int test = fixture.Test.CallCount;
                int reviewer = fixture.Reviewer.CallCount;

                JobOperationResult result = await fixture.Orchestrator
                        .ApproveHumanReviewAsync(job.Id, "human-review", "approve-1");

                Assert.True(result.IsSuccess);
                Assert.Equal(JobState.Completed, job.State);
                Assert.False(job.State == JobState.WaitingHuman);
                ArtifactWriteRequest write = Assert.Single(fixture.Artifacts.Writes);
                Assert.Equal(ArtifactType.HumanReviewApprovalEvidence, write.ArtifactType);
                HumanReviewApprovalEvidence evidence = JsonSerializer.Deserialize<HumanReviewApprovalEvidence>(
                        write.Content.Span)!;
                Assert.Equal("human-review", evidence.Actor);
                Assert.Equal("approve-1", evidence.CorrelationId);
                Assert.Equal(job.Id, evidence.JobId);
                Assert.Equal(1, evidence.AttemptCount);
                Assert.Equal(64, evidence.ReviewerReviewSha256.Length);
                Assert.Equal(64, evidence.DeterministicAcceptanceGateSha256.Length);
                Assert.Equal(developer, fixture.Developer.CallCount);
                Assert.Equal(safeChange, fixture.SafeChange.CallCount);
                Assert.Equal(build, fixture.Build.CallCount);
                Assert.Equal(test, fixture.Test.CallCount);
                Assert.Equal(reviewer, fixture.Reviewer.CallCount);
        }

        [Theory]
        [InlineData(DeveloperProposalLineage.Original,
                RecoveryStage.ReviewerOriginal,
                RecoveryStage.ObservedChanges,
                RecoveryStage.Build,
                RecoveryStage.Test)]
        [InlineData(DeveloperProposalLineage.BuildCorrection,
                RecoveryStage.ReviewerOriginal,
                RecoveryStage.ObservedChanges,
                RecoveryStage.Build,
                RecoveryStage.Test)]
        [InlineData(DeveloperProposalLineage.HumanReviewCorrection,
                RecoveryStage.ReviewerHumanReviewCorrectionSourceAwareSuperseding,
                RecoveryStage.ObservedHumanReviewCorrection,
                RecoveryStage.BuildHumanReviewCorrection,
                RecoveryStage.TestHumanReviewCorrection)]
        [InlineData(DeveloperProposalLineage.GovernedHumanCorrection,
                RecoveryStage.ReviewerOriginal,
                RecoveryStage.ObservedGovernedHumanCorrection,
                RecoveryStage.BuildGovernedHumanCorrection,
                RecoveryStage.TestGovernedHumanCorrection)]
        public async Task Human_review_approval_selects_final_effective_lineage(
                DeveloperProposalLineage lineage,
                RecoveryStage reviewerStage,
                RecoveryStage observedStage,
                RecoveryStage buildStage,
                RecoveryStage testStage)
        {
                (Fixture fixture, Job job) = await WaitingHumanJob(
                        $"KRX-APPROVE-{lineage}");
                ConfigureApprovalEvidence(fixture, job, lineage: lineage);

                JobOperationResult result = await fixture.Orchestrator
                        .ApproveHumanReviewAsync(
                                job.Id,
                                "human-review",
                                $"approve-{lineage}");

                Assert.True(result.IsSuccess);
                Assert.Equal(JobState.Completed, job.State);
                Assert.Contains(fixture.RecoveryEvidence.Requests,
                        request => request.Stage == reviewerStage);
                Assert.Contains(fixture.RecoveryEvidence.Requests,
                        request => request.Stage == observedStage);
                Assert.Contains(fixture.RecoveryEvidence.Requests,
                        request => request.Stage == buildStage);
                Assert.Contains(fixture.RecoveryEvidence.Requests,
                        request => request.Stage == testStage);
                if (lineage == DeveloperProposalLineage.GovernedHumanCorrection)
                        Assert.DoesNotContain(fixture.RecoveryEvidence.Requests,
                                request => request.Stage ==
                                    RecoveryStage.ReviewerHumanReviewCorrectionSourceAwareSuperseding);

                HumanReviewApprovalEvidence evidence = JsonSerializer
                        .Deserialize<HumanReviewApprovalEvidence>(
                                Assert.Single(fixture.Artifacts.Writes)
                                    .Content.Span)!;
                Assert.Equal(lineage, evidence.EffectiveProposalLineage);
                Assert.All(new[]
                {
                        evidence.EffectiveProposalSha256,
                        evidence.ObservedChangesSha256,
                        evidence.BuildReportSha256,
                        evidence.TestReportSha256,
                        evidence.EffectiveSourceSnapshotSha256
                }, hash => Assert.Equal(64, hash.Length));
        }

        [Fact]
        public async Task Human_review_approval_uses_latest_governed_source_independently_of_operation_correlation()
        {
                (Fixture fixture, Job job) = await WaitingHumanJob(
                        "KRX-APPROVE-SEQUENTIAL-GOVERNED");
                ConfigureApprovalEvidence(
                        fixture,
                        job,
                        lineage: DeveloperProposalLineage.GovernedHumanCorrection);

                Guid runId = new DeterministicJobRunIdProvider().Create(
                        job.Id,
                        job.AttemptCount);
                const string sourceCorrelationId = "governed-human-correction-02";
                const string approvalCorrelationId = "human-review-approval-v1";
                string testCorrelationId = job.Transitions
                        .Where(transition =>
                                transition.FromState == JobState.Testing &&
                                transition.ToState == JobState.Reviewing)
                        .OrderByDescending(transition => transition.OccurredOnUtc)
                        .Select(transition => transition.CorrelationId)
                        .First();
                DateTimeOffset recordedAt = DateTimeOffset.UtcNow;
                AddArtifact(ArtifactType.GovernedHumanCorrectionRequest,
                        sourceCorrelationId, recordedAt);
                AddArtifact(ArtifactType.ObservedGovernedHumanCorrectionManifest,
                        sourceCorrelationId, recordedAt.AddSeconds(1));
                AddArtifact(ArtifactType.BuildGovernedHumanCorrectionReport,
                        sourceCorrelationId, recordedAt.AddSeconds(2));
                AddArtifact(ArtifactType.TestGovernedHumanCorrectionReport,
                        testCorrelationId, recordedAt.AddSeconds(3));

                JobOperationResult result = await fixture.Orchestrator
                        .ApproveHumanReviewAsync(
                                job.Id,
                                "human-review",
                                approvalCorrelationId);

                Assert.True(result.IsSuccess);
                Assert.Equal(JobState.Completed, job.State);
                Assert.Contains(fixture.RecoveryEvidence.Requests,
                        request => request.Stage == RecoveryStage.EffectiveDeveloperProposal &&
                                request.CorrelationId == sourceCorrelationId);
                Assert.Contains(fixture.RecoveryEvidence.Requests,
                        request => request.Stage == RecoveryStage.ObservedGovernedHumanCorrection &&
                                request.CorrelationId == sourceCorrelationId);
                Assert.Contains(fixture.RecoveryEvidence.Requests,
                        request => request.Stage == RecoveryStage.BuildGovernedHumanCorrection &&
                                request.CorrelationId == sourceCorrelationId);
                Assert.Contains(fixture.RecoveryEvidence.Requests,
                        request => request.Stage == RecoveryStage.TestGovernedHumanCorrection &&
                                request.CorrelationId == testCorrelationId);
                Assert.Contains(fixture.RecoveryEvidence.Requests,
                        request => request.Stage == RecoveryStage.HumanReviewApproval &&
                                request.CorrelationId == approvalCorrelationId);

                void AddArtifact(
                        ArtifactType artifactType,
                        string correlationId,
                        DateTimeOffset createdAt) =>
                        fixture.ArtifactMetadata.Records.Add(new ArtifactRecord
                        {
                                ArtifactId = Guid.NewGuid(),
                                JobId = job.Id,
                                RunId = runId,
                                ArtifactType = artifactType,
                                RelativePath = $"test/{artifactType}.json",
                                Sha256 = new string('a', 64),
                                SizeBytes = 1,
                                CreatedAtUtc = createdAt,
                                CorrelationId = correlationId
                        });
        }

        [Theory]
        [InlineData(RecoveryStage.ReviewerOriginal)]
        [InlineData(RecoveryStage.ObservedGovernedHumanCorrection)]
        [InlineData(RecoveryStage.BuildGovernedHumanCorrection)]
        [InlineData(RecoveryStage.TestGovernedHumanCorrection)]
        public async Task Governed_human_review_approval_fails_closed_when_selected_evidence_is_missing(
                RecoveryStage missing)
        {
                (Fixture fixture, Job job) = await WaitingHumanJob(
                        "KRX-APPROVE-GOVERNED-MISSING");
                ConfigureApprovalEvidence(
                        fixture,
                        job,
                        missing: missing,
                        lineage: DeveloperProposalLineage.GovernedHumanCorrection);

                JobOperationResult result = await fixture.Orchestrator
                        .ApproveHumanReviewAsync(
                                job.Id,
                                "human-review",
                                "approve-governed-missing");

                Assert.False(result.IsSuccess);
                Assert.Equal(JobState.WaitingHuman, job.State);
                Assert.Empty(fixture.Artifacts.Writes);
        }

        [Fact]
        public async Task Human_review_approval_is_idempotent_for_same_correlation()
        {
                (Fixture fixture, Job job) = await WaitingHumanJob("KRX-APPROVE-IDEMPOTENT");
                ConfigureApprovalEvidence(fixture, job, approvalExistsAfterWrite: true);

                Assert.True((await fixture.Orchestrator.ApproveHumanReviewAsync(
                        job.Id, "human-review", "approve-idempotent")).IsSuccess);
                int transitions = job.Transitions.Count;
                Assert.True((await fixture.Orchestrator.ApproveHumanReviewAsync(
                        job.Id, "human-review", "approve-idempotent")).IsSuccess);

                Assert.Single(fixture.Artifacts.Writes);
                Assert.Equal(transitions, job.Transitions.Count);
        }

        [Theory]
        [InlineData(ReviewerDecision.ChangesRequired, false)]
        [InlineData(ReviewerDecision.Approved, true)]
        public async Task Human_review_approval_rejects_non_approvable_review(
                ReviewerDecision decision, bool corrections)
        {
                (Fixture fixture, Job job) = await WaitingHumanJob("KRX-APPROVE-REVIEW");
                ConfigureApprovalEvidence(fixture, job, decision: decision,
                        hasCorrections: corrections);

                JobOperationResult result = await fixture.Orchestrator
                        .ApproveHumanReviewAsync(job.Id, "human-review", "approve-review");

                Assert.False(result.IsSuccess);
                Assert.Equal(JobState.WaitingHuman, job.State);
                Assert.Empty(fixture.Artifacts.Writes);
        }

        [Fact]
        public async Task Human_review_approval_rejects_failed_deterministic_gate()
        {
                (Fixture fixture, Job job) = await WaitingHumanJob("KRX-APPROVE-GATE");
                ConfigureApprovalEvidence(fixture, job, gatePass: false);

                JobOperationResult result = await fixture.Orchestrator
                        .ApproveHumanReviewAsync(job.Id, "human-review", "approve-gate");

                Assert.False(result.IsSuccess);
                Assert.Equal(JobState.WaitingHuman, job.State);
                Assert.Empty(fixture.Artifacts.Writes);
        }

        [Theory]
        [InlineData(RecoveryStage.ReviewerHumanReviewCorrectionSourceAwareSuperseding)]
        [InlineData(RecoveryStage.ObservedHumanReviewCorrection)]
        [InlineData(RecoveryStage.BuildHumanReviewCorrection)]
        [InlineData(RecoveryStage.TestHumanReviewCorrection)]
        public async Task Human_review_approval_rejects_missing_evidence(
                RecoveryStage missing)
        {
                (Fixture fixture, Job job) = await WaitingHumanJob("KRX-APPROVE-MISSING");
                ConfigureApprovalEvidence(fixture, job, missing: missing);

                JobOperationResult result = await fixture.Orchestrator
                        .ApproveHumanReviewAsync(job.Id, "human-review", "approve-missing");

                Assert.False(result.IsSuccess);
                Assert.Equal(JobState.WaitingHuman, job.State);
                Assert.Empty(fixture.Artifacts.Writes);
        }

        [Fact]
        public async Task Human_review_approval_rejects_state_other_than_waiting_human()
        {
                Fixture fixture = CreateFixture();
                Result<Job> created = await fixture.Service.CreateAsync(
                        "request", "KRX-APPROVE-STATE");

                JobOperationResult result = await fixture.Orchestrator
                        .ApproveHumanReviewAsync(created.Value.Id, "human-review", "approve-state");

                Assert.False(result.IsSuccess);
                Assert.Equal(JobState.Created, created.Value.State);
                Assert.Empty(fixture.Artifacts.Writes);
        }

        private static async Task<(Fixture Fixture, Job Job)> WaitingHumanJob(string externalId)
        {
                Fixture fixture = CreateFixture();
                Result<Job> created = await fixture.Service.CreateAsync("request", externalId);
                fixture.RecoveryEvidence.Handler = request => ReviewerEvidence(
                        request, created.Value.BaseRepositoryHead ?? fixture.SourceRevision.Head);
                int guard = 0;
                while (created.Value.State != JobState.WaitingHuman && guard++ < 12)
                        Assert.True((await fixture.Orchestrator.AdvanceAsync(
                                created.Value.Id, "orchestrator", "initial")).IsSuccess);
                Assert.Equal(JobState.WaitingHuman, created.Value.State);
                fixture.Artifacts.Writes.Clear();
                return (fixture, created.Value);
        }

        private static void ConfigureApprovalEvidence(
                Fixture fixture,
                Job job,
                bool approvalExistsAfterWrite = false,
                ReviewerDecision decision = ReviewerDecision.Approved,
                bool hasCorrections = false,
                bool gatePass = true,
                RecoveryStage? missing = null,
                DeveloperProposalLineage lineage =
                        DeveloperProposalLineage.HumanReviewCorrection)
        {
                Guid runId = new DeterministicJobRunIdProvider().Create(job.Id, job.AttemptCount);
                DeveloperChangeOperationType operation = lineage ==
                        DeveloperProposalLineage.Original
                                ? DeveloperChangeOperationType.CreateFile
                                : DeveloperChangeOperationType.ReplaceFile;
                const string content = "class Generated {}";
                string contentSha = Convert.ToHexString(
                        System.Security.Cryptography.SHA256.HashData(
                                System.Text.Encoding.UTF8.GetBytes(content)))
                    .ToLowerInvariant();
                var proposal = new ValidatedDeveloperProposal(
                        "approval proposal",
                        [new ValidatedDeveloperChange(
                                operation,
                                "src/generated.cs",
                                "approval",
                                content,
                                operation == DeveloperChangeOperationType.ReplaceFile
                                        ? new string('b', 64)
                                        : string.Empty,
                                content.Length)],
                        [], [], content.Length, content.Length + 100);
                var criterion = new DeterministicAcceptanceCriterionResult(
                        new DeterministicAcceptanceCriterion(
                                "file", DeterministicCriterionKind.FileExists,
                                "src/generated.cs", "src/generated.cs", "candidate file exists"),
                        gatePass ? DeterministicCriterionStatus.Pass : DeterministicCriterionStatus.Fail,
                        gatePass ? $"sha256:{new string('a', 64)}" : "missing");
                var review = new ReviewerReview
                {
                        Decision = decision,
                        Findings = [],
                        RequiredCorrections = hasCorrections
                                ? [new ReviewerCorrection
                                    { RelativePath = "src/generated.cs", Instruction = "fix" }]
                                : [],
                        RiskAssessment = "low",
                        Summary = "review",
                        DeterministicAcceptanceGate = new(
                                [criterion], ["architecture follows conventions"])
                };
                var observed = new ObservedChangeManifest(
                        job.Id,
                        runId,
                        job.BaseRepositoryHead!,
                        [new ObservedChangeManifestEntry(
                                "src/generated.cs",
                                operation == DeveloperChangeOperationType.CreateFile
                                        ? ObservedRepositoryChangeKind.Created
                                        : ObservedRepositoryChangeKind.Modified,
                                contentSha,
                                content.Length,
                                operation,
                                operation == DeveloperChangeOperationType.CreateFile
                                        ? ObservedRepositoryChangeKind.Created
                                        : ObservedRepositoryChangeKind.Modified,
                                operation == DeveloperChangeOperationType.ReplaceFile
                                        ? new string('b', 64)
                                        : null,
                                "PASS")],
                        job.AttemptCount,
                        lineage == DeveloperProposalLineage.GovernedHumanCorrection
                                ? "governed-human-correction:approval-governed"
                                : string.Empty,
                        lineage == DeveloperProposalLineage.GovernedHumanCorrection
                                ? SafeChangeProposalIdentity.Fingerprint(proposal)
                                : string.Empty,
                        lineage == DeveloperProposalLineage.GovernedHumanCorrection
                                ? "governed-human-correction:approval-governed"
                                : string.Empty);
                var governedEvidence = new GovernedHumanCorrectionEvidence
                {
                        JobId = job.Id,
                        RunId = runId,
                        AttemptCount = job.AttemptCount,
                        Stage = "Building",
                        Actor = "human-review",
                        CorrelationId = "approval-governed",
                        Reason = "test",
                        RequestSha256 = new string('c', 64),
                        FailureEvidence = ["build/report.json"],
                        ExhaustedAiCorrectionEvidence = ["developer/rejected.json"],
                        AllowedPaths = ["src/generated.cs"],
                        Proposal = proposal,
                        RecordedAtUtc = DateTimeOffset.UtcNow
                };
                var governedReceipt = new GovernedHumanCorrectionReceipt
                {
                        JobId = job.Id,
                        RunId = runId,
                        AttemptCount = job.AttemptCount,
                        Actor = "human-review",
                        CorrelationId = "approval-governed",
                        RequestSha256 = governedEvidence.RequestSha256,
                        Changes = [new AppliedFileChange(
                                operation,
                                "src/generated.cs",
                                new string('b', 64),
                                contentSha,
                                content.Length)],
                        RecordedAtUtc = DateTimeOffset.UtcNow
                };
                RecoveryStage reviewerStage = lineage ==
                        DeveloperProposalLineage.HumanReviewCorrection
                                ? RecoveryStage.ReviewerHumanReviewCorrectionSourceAwareSuperseding
                                : RecoveryStage.ReviewerOriginal;
                RecoveryStage observedStage = lineage switch
                {
                        DeveloperProposalLineage.HumanReviewCorrection =>
                                RecoveryStage.ObservedHumanReviewCorrection,
                        DeveloperProposalLineage.GovernedHumanCorrection =>
                                RecoveryStage.ObservedGovernedHumanCorrection,
                        _ => RecoveryStage.ObservedChanges
                };
                RecoveryStage buildStage = lineage switch
                {
                        DeveloperProposalLineage.HumanReviewCorrection =>
                                RecoveryStage.BuildHumanReviewCorrection,
                        DeveloperProposalLineage.GovernedHumanCorrection =>
                                RecoveryStage.BuildGovernedHumanCorrection,
                        _ => RecoveryStage.Build
                };
                RecoveryStage testStage = lineage switch
                {
                        DeveloperProposalLineage.HumanReviewCorrection =>
                                RecoveryStage.TestHumanReviewCorrection,
                        DeveloperProposalLineage.GovernedHumanCorrection =>
                                RecoveryStage.TestGovernedHumanCorrection,
                        _ => RecoveryStage.Test
                };
                fixture.RecoveryEvidence.Handler = request =>
                {
                        if (request.Stage == RecoveryStage.HumanReviewApproval)
                                return approvalExistsAfterWrite &&
                                    fixture.Artifacts.Writes.Any(write =>
                                        write.ArtifactType == ArtifactType.HumanReviewApprovalEvidence)
                                        ? StageRecoveryResult.Completed()
                                        : StageRecoveryResult.NotCompleted();
                        if (request.Stage == missing) return StageRecoveryResult.NotCompleted();
                        if (request.Stage == RecoveryStage.Planning)
                                return ReviewerEvidence(
                                        request,
                                        job.BaseRepositoryHead!).PlannerPlan is { } plan
                                        ? StageRecoveryResult.Completed(plannerPlan: plan)
                                        : StageRecoveryResult.NotCompleted();
                        if (request.Stage == RecoveryStage.EffectiveDeveloperProposal)
                                return StageRecoveryResult.Completed(
                                        developerProposal: proposal,
                                        developerProposalLineage: lineage,
                                        governedHumanCorrection: lineage ==
                                                DeveloperProposalLineage.GovernedHumanCorrection
                                                        ? governedEvidence
                                                        : null,
                                        governedHumanCorrectionReceipt: lineage ==
                                                DeveloperProposalLineage.GovernedHumanCorrection
                                                ? governedReceipt
                                                        : null);
                        if (request.Stage == RecoveryStage.GovernedHumanCorrection &&
                            lineage == DeveloperProposalLineage.GovernedHumanCorrection)
                                return StageRecoveryResult.Completed(
                                        developerProposal: proposal,
                                        developerProposalLineage: lineage,
                                        governedHumanCorrection: governedEvidence,
                                        governedHumanCorrectionReceipt: governedReceipt);
                        if (request.Stage == reviewerStage)
                                return StageRecoveryResult.Completed(reviewerReview: review);
                        if (request.Stage == observedStage)
                                return StageRecoveryResult.Completed(
                                        observedChangeManifest: observed);
                        if (request.Stage == buildStage)
                                return StageRecoveryResult.Completed(buildReport:
                                        new BuildExecutionReport(job.Id, runId, "target",
                                                ToolExecutionOutcome.Completed, 0, "",
                                                DateTime.UtcNow, DateTime.UtcNow, TimeSpan.Zero));
                        if (request.Stage == testStage)
                                return StageRecoveryResult.Completed(testReport:
                                        new TestExecutionReport(job.Id, runId, "target",
                                                ToolExecutionOutcome.Completed, 0, "",
                                                DateTime.UtcNow, DateTime.UtcNow, TimeSpan.Zero));
                        return StageRecoveryResult.NotCompleted();
                };
        }

        [Fact]
        public async Task Superseding_human_review_is_reviewer_only_and_idempotent()
        {
                Fixture fixture = CreateFixture();
                Result<Job> created = await fixture.Service.CreateAsync(
                        "request", "KRX-SUPERSEDE");
                fixture.RecoveryEvidence.Handler = request => ReviewerEvidence(
                        request,
                        created.Value.BaseRepositoryHead ?? fixture.SourceRevision.Head);
                int guard = 0;
                while (created.Value.State != JobState.WaitingHuman && guard++ < 12)
                        Assert.True((await fixture.Orchestrator.AdvanceAsync(
                                created.Value.Id, "orchestrator", "initial")).IsSuccess);

                int reviewerCallsBefore = fixture.Reviewer.CallCount;
                int developerCallsBefore = fixture.Developer.CallCount;
                int buildCallsBefore = fixture.Build.CallCount;
                int testCallsBefore = fixture.Test.CallCount;
                Guid runId = new DeterministicJobRunIdProvider().Create(
                        created.Value.Id, created.Value.AttemptCount);
                var correction = new HumanReviewCorrectionEvidence
                {
                        JobId = created.Value.Id,
                        RunId = runId,
                        Decision = HumanReviewDecision.ChangesRequired,
                        RequiredCorrections =
                        [
                                new HumanReviewRequiredCorrection
                                {
                                        RelativePath = "src/generated.cs",
                                        Instruction = "Apply the human finding."
                                }
                        ],
                        RecordedAtUtc = DateTimeOffset.UtcNow
                };
                var effectiveProposal = new ValidatedDeveloperProposal(
                        "Human correction",
                        [
                                new ValidatedDeveloperChange(
                                        DeveloperChangeOperationType.ReplaceFile,
                                        "src/generated.cs",
                                        "Apply finding",
                                        "class Generated { int HumanFixed; }",
                                        new string('a', 64),
                                        41)
                        ],
                        [], [], 41, 100);

                fixture.RecoveryEvidence.Handler = request => request.Stage switch
                {
                        RecoveryStage.ReviewerHumanReviewCorrectionSourceAwareSuperseding =>
                                fixture.Reviewer.CallCount > reviewerCallsBefore
                                        ? StageRecoveryResult.Completed(
                                                reviewerReview: fixture.Reviewer.Review)
                                        : StageRecoveryResult.NotCompleted(),
                        RecoveryStage.ReviewerHumanReviewCorrection =>
                                StageRecoveryResult.Completed(
                                        reviewerReview: fixture.Reviewer.Review with
                                        {
                                                Decision = ReviewerDecision.ChangesRequired
                                        }),
                        RecoveryStage.HumanReviewCorrection =>
                                StageRecoveryResult.Completed(
                                        humanReviewCorrection: correction),
                        RecoveryStage.EffectiveDeveloperProposal =>
                                StageRecoveryResult.Completed(
                                        developerProposal: effectiveProposal,
                                        developerProposalLineage:
                                            DeveloperProposalLineage.HumanReviewCorrection),
                        RecoveryStage.ObservedHumanReviewCorrection =>
                                StageRecoveryResult.Completed(
                                        observedChangeManifest: new ObservedChangeManifest(
                                                created.Value.Id, runId,
                                                created.Value.BaseRepositoryHead!, [])),
                        RecoveryStage.BuildHumanReviewCorrection =>
                                StageRecoveryResult.Completed(
                                        buildReport: new BuildExecutionReport(
                                                created.Value.Id, runId, "target",
                                                ToolExecutionOutcome.Completed, 0, "",
                                                DateTime.UtcNow, DateTime.UtcNow,
                                                TimeSpan.Zero)),
                        RecoveryStage.TestHumanReviewCorrection =>
                                StageRecoveryResult.Completed(
                                        testReport: new TestExecutionReport(
                                                created.Value.Id, runId, "target",
                                                ToolExecutionOutcome.Completed, 0, "",
                                                DateTime.UtcNow, DateTime.UtcNow,
                                                TimeSpan.Zero)),
                        _ => ReviewerEvidence(
                                request, created.Value.BaseRepositoryHead!)
                };

                JobOperationResult first = await fixture.Orchestrator
                        .SupersedeHumanReviewCorrectionReviewAsync(
                                created.Value.Id, "human", "supersede-1");
                JobOperationResult second = await fixture.Orchestrator
                        .SupersedeHumanReviewCorrectionReviewAsync(
                                created.Value.Id, "human", "supersede-2");

                Assert.True(first.IsSuccess);
                Assert.True(second.IsSuccess);
                Assert.Equal(JobState.WaitingHuman, created.Value.State);
                Assert.Equal(reviewerCallsBefore + 1, fixture.Reviewer.CallCount);
                Assert.Equal(developerCallsBefore, fixture.Developer.CallCount);
                Assert.Equal(buildCallsBefore, fixture.Build.CallCount);
                Assert.Equal(testCallsBefore, fixture.Test.CallCount);
                Assert.True(fixture.Reviewer.LastRequest?
                        .IsSupersedingHumanReviewCorrection);
                Assert.Equal(3,
                        fixture.Reviewer.LastRequest?.SupersedingReviewVersion);
                Assert.Equal(DeveloperProposalLineage.HumanReviewCorrection,
                        fixture.Reviewer.LastRequest?.EffectiveProposalLineage);
                Assert.Contains("HumanFixed", Assert.Single(
                        fixture.Reviewer.LastRequest!.DeveloperProposal.Changes)
                    .Content);
        }

        [Fact]
        public async Task Human_review_correction_rejects_invalid_job_state()
        {
                Fixture fixture = CreateFixture();
                Result<Job> created = await fixture.Service.CreateAsync("request", "KRX-HUMAN-STATE");

                JobOperationResult result = await fixture.Orchestrator.RequestHumanReviewCorrectionAsync(
                        created.Value.Id,
                        new HumanReviewCorrectionRequest
                        {
                                Actor = "human", CorrelationId = "invalid-state",
                                RequiredCorrections =
                                [
                                        new HumanReviewRequiredCorrection
                                        {
                                                RelativePath = "src/generated.cs",
                                                Instruction = "not valid while queued"
                                        }
                                ]
                        });

                Assert.False(result.IsSuccess);
                Assert.Equal(JobOperationKind.InvalidTransition, result.Kind);
                Assert.Empty(fixture.Artifacts.Writes);
                Assert.Equal(JobState.Created, created.Value.State);
        }

        [Fact]
        public async Task Human_review_correction_rejects_terminal_job()
        {
                Fixture fixture = CreateFixture();
                Result<Job> created = await fixture.Service.CreateAsync("request", "KRX-HUMAN-TERMINAL");
                fixture.RecoveryEvidence.Handler = request => ReviewerEvidence(
                        request, created.Value.BaseRepositoryHead ?? fixture.SourceRevision.Head);
                int guard = 0;
                while (created.Value.State != JobState.WaitingHuman && guard++ < 12)
                        Assert.True((await fixture.Orchestrator.AdvanceAsync(
                                created.Value.Id, "orchestrator", "initial")).IsSuccess);
                Assert.True(created.Value.TransitionTo(
                        JobState.Completed, fixture.Clock.UtcNow,
                        "human accepted", "human", "complete").IsSuccess);

                JobOperationResult result = await fixture.Orchestrator.RequestHumanReviewCorrectionAsync(
                        created.Value.Id,
                        new HumanReviewCorrectionRequest
                        {
                                Actor = "human", CorrelationId = "terminal",
                                RequiredCorrections =
                                [new HumanReviewRequiredCorrection
                                {
                                        RelativePath = "src/generated.cs",
                                        Instruction = "must be rejected"
                                }]
                        });

                Assert.False(result.IsSuccess);
                Assert.Equal(JobOperationKind.InvalidTransition, result.Kind);
                Assert.Empty(fixture.Artifacts.Writes);
                Assert.Equal(JobState.Completed, created.Value.State);
        }

        [Fact]
        public void Active_human_review_cycle_is_detected_in_ascending_order()
        {
                DateTime now = UtcNow;
                JobTransition[] transitions =
                [
                        Transition(JobState.Reviewing, JobState.WaitingHuman, now.AddMinutes(-2)),
                        Transition(JobState.WaitingHuman, JobState.Developing, now.AddMinutes(-1))
                ];

                Assert.True(JobOrchestrator.HasActiveHumanReviewCorrection(
                        JobState.Developing, transitions));
        }

        [Fact]
        public void Active_human_review_cycle_is_detected_in_descending_order()
        {
                DateTime now = UtcNow;
                JobTransition[] transitions =
                [
                        Transition(JobState.WaitingHuman, JobState.Developing, now.AddMinutes(-1)),
                        Transition(JobState.Reviewing, JobState.WaitingHuman, now.AddMinutes(-2))
                ];

                Assert.True(JobOrchestrator.HasActiveHumanReviewCorrection(
                        JobState.Developing, transitions));
        }

        [Fact]
        public void Historical_waiting_human_does_not_invalidate_newer_cycle()
        {
                DateTime now = UtcNow;
                JobTransition[] transitions =
                [
                        Transition(JobState.WaitingHuman, JobState.Developing, now),
                        Transition(JobState.Reviewing, JobState.WaitingHuman, now.AddDays(-2)),
                        Transition(JobState.Testing, JobState.Reviewing, now.AddDays(-3))
                ];

                Assert.True(JobOrchestrator.HasActiveHumanReviewCorrection(
                        JobState.Building, transitions));
        }

        [Fact]
        public void Completed_or_missing_human_review_cycle_is_inactive()
        {
                DateTime now = UtcNow;
                JobTransition[] completed =
                [
                        Transition(JobState.WaitingHuman, JobState.Developing, now.AddMinutes(-2)),
                        Transition(JobState.Reviewing, JobState.WaitingHuman, now.AddMinutes(-1))
                ];

                Assert.False(JobOrchestrator.HasActiveHumanReviewCorrection(
                        JobState.WaitingHuman, completed));
                Assert.False(JobOrchestrator.HasActiveHumanReviewCorrection(
                        JobState.Developing, []));
        }

        [Fact]
        public async Task Active_human_review_cycle_without_evidence_fails_closed()
        {
                Fixture fixture = CreateFixture();
                Result<Job> created = await fixture.Service.CreateAsync("request", "KRX-HUMAN-MISSING-EVIDENCE");
                fixture.RecoveryEvidence.Handler = request => ReviewerEvidence(
                        request, created.Value.BaseRepositoryHead ?? fixture.SourceRevision.Head);
                int guard = 0;
                while (created.Value.State != JobState.WaitingHuman && guard++ < 12)
                        Assert.True((await fixture.Orchestrator.AdvanceAsync(
                                created.Value.Id, "orchestrator", "initial")).IsSuccess);

                fixture.RecoveryEvidence.Handler = request => request.Stage switch
                {
                        RecoveryStage.HumanReviewCorrection => StageRecoveryResult.NotCompleted(),
                        RecoveryStage.ReviewerOriginal => StageRecoveryResult.Completed(
                                reviewerReview: fixture.Reviewer.Review),
                        _ => ReviewerEvidence(request, created.Value.BaseRepositoryHead!)
                };
                Assert.True((await fixture.Orchestrator.RequestHumanReviewCorrectionAsync(
                        created.Value.Id,
                        new HumanReviewCorrectionRequest
                        {
                                Actor = "human", CorrelationId = "missing-evidence",
                                RequiredCorrections =
                                [new HumanReviewRequiredCorrection
                                {
                                        RelativePath = "src/generated.cs",
                                        Instruction = "correct it"
                                }]
                        })).IsSuccess);

                int developerCalls = fixture.Developer.CallCount;
                int writes = fixture.Artifacts.Writes.Count;
                fixture.RecoveryEvidence.Handler = request =>
                        request.Stage == RecoveryStage.HumanReviewCorrection
                                ? StageRecoveryResult.NotCompleted()
                                : ReviewerEvidence(request, created.Value.BaseRepositoryHead!);

                JobOperationResult result = await fixture.Orchestrator.AdvanceAsync(
                        created.Value.Id, "orchestrator", "missing-evidence-advance");

                Assert.False(result.IsSuccess);
                Assert.Equal(JobApplicationErrors.StageRecoveryFailed, result.Error);
                Assert.Equal(JobState.Developing, created.Value.State);
                Assert.Equal(developerCalls, fixture.Developer.CallCount);
                Assert.Equal(writes, fixture.Artifacts.Writes.Count);
        }

        [Fact]
        public void Human_review_cycle_detection_is_idempotent_and_side_effect_free()
        {
                Fixture fixture = CreateFixture();
                DateTime now = UtcNow;
                JobTransition[] transitions =
                [
                        Transition(JobState.WaitingHuman, JobState.Developing, now),
                        Transition(JobState.Reviewing, JobState.WaitingHuman, now.AddMinutes(-1))
                ];

                Assert.True(JobOrchestrator.HasActiveHumanReviewCorrection(
                        JobState.Developing, transitions));
                Assert.True(JobOrchestrator.HasActiveHumanReviewCorrection(
                        JobState.Developing, transitions.Reverse().ToArray()));
                Assert.Equal(0, fixture.Developer.CallCount);
                Assert.Empty(fixture.Artifacts.Writes);
        }

        private static JobTransition Transition(
                JobState from,
                JobState to,
                DateTime occurredOnUtc) =>
                new(from, to, occurredOnUtc, "test", "test", "test");

        private static StageRecoveryResult ReviewerEvidence(StageRecoveryRequest request,string head)
        {
                return request.Stage switch
                {
                        RecoveryStage.Planning => StageRecoveryResult.Completed(plannerPlan:new PlannerPlan
                        { Objective="o",FilesToInspect=[],CandidateFilesToModify=["src/generated.cs"],Strategy="s",
                          AcceptanceCriteria=[],Risks=[],ExpectedTests=[],Assumptions=[],Uncertainties=[] }),
                        RecoveryStage.Developer => StageRecoveryResult.Completed(developerProposal:
                            new ValidatedDeveloperProposal("s",
                                [new ValidatedDeveloperChange(
                                    DeveloperChangeOperationType.CreateFile,
                                    "src/generated.cs", "create",
                                    "class Generated {}", string.Empty, 18)],
                                [],[],18,100)),
                        RecoveryStage.EffectiveDeveloperProposal => StageRecoveryResult.Completed(
                            developerProposal: new ValidatedDeveloperProposal("s",
                                [new ValidatedDeveloperChange(
                                    DeveloperChangeOperationType.CreateFile,
                                    "src/generated.cs", "create",
                                    "class Generated {}", string.Empty, 18)],
                                [],[],18,100),
                            developerProposalLineage: DeveloperProposalLineage.Original),
                        RecoveryStage.ObservedChanges => StageRecoveryResult.Completed(observedChangeManifest:
                            new ObservedChangeManifest(request.JobId,request.RunId,head,[])),
                        RecoveryStage.Build => StageRecoveryResult.Completed(buildReport:
                            new BuildExecutionReport(request.JobId,request.RunId,"target",ToolExecutionOutcome.Completed,0,"",
                                DateTime.UtcNow,DateTime.UtcNow,TimeSpan.Zero)),
                        RecoveryStage.Test => StageRecoveryResult.Completed(testReport:
                            new TestExecutionReport(request.JobId,request.RunId,"target",ToolExecutionOutcome.Completed,0,"",
                                DateTime.UtcNow,DateTime.UtcNow,TimeSpan.Zero)),
                        RecoveryStage.Reviewer => StageRecoveryResult.NotCompleted(),
                        _ => StageRecoveryResult.NotCompleted()
                };
        }

        private sealed class FakeStageRecoveryEvidenceService :
                IStageRecoveryEvidenceService
        {
                public int CallCount { get; private set; }

                public List<StageRecoveryRequest> Requests
                {
                        get;
                } = [];

                public StageRecoveryResult Result
                {
                        get;
                        set;
                } = StageRecoveryResult.NotCompleted();

                public Func<
                        StageRecoveryRequest,
                        StageRecoveryResult>? Handler
                {
                        get;
                        set;
                }

                public Task<StageRecoveryResult> CheckAsync(
                        StageRecoveryRequest request,
                        CancellationToken cancellationToken = default)
                {
                        cancellationToken.ThrowIfCancellationRequested();

                        CallCount++;
                        Requests.Add(request);

                        StageRecoveryResult result =
                                Handler?.Invoke(request) ??
                                Result;

                        return Task.FromResult(result);
                }
        }

        private sealed class FakePlanningExecutionService :
                IPlanningExecutionService
        {
                public int CallCount { get; private set; }

                public PlanningExecutionResult Result { get; set; } =
                        PlanningExecutionResult.Success(
                                new PlanningExecutionReport
                                {
                                        JobId = Guid.NewGuid(),
                                        RunId = Guid.NewGuid(),
                                        LogicalModel = "CodingQuality",
                                        Provider = "Fake",
                                        PhysicalModel = "fake-model",
                                        Duration = TimeSpan.Zero,
                                        TerminationReason = "Stop"
                                },
                                new ArtifactRecord
                                {
                                        ArtifactId = Guid.NewGuid(),
                                        JobId = Guid.NewGuid(),
                                        RunId = Guid.NewGuid(),
                                        ArtifactType =
                                                ArtifactType.AiResponse,
                                        RelativePath =
                                                "ai/response.json",
                                        Sha256 =
                                                new string('d', 64),
                                        SizeBytes = 1,
                                        CreatedAtUtc =
                                                DateTimeOffset.UtcNow,
                                        CorrelationId = "test"
                                },
                                new ArtifactRecord
                                {
                                        ArtifactId = Guid.NewGuid(),
                                        JobId = Guid.NewGuid(),
                                        RunId = Guid.NewGuid(),
                                        ArtifactType =
                                                ArtifactType.PlanningPlan,
                                        RelativePath =
                                                "planner/plan.json",
                                        Sha256 =
                                                new string('e', 64),
                                        SizeBytes = 1,
                                        CreatedAtUtc =
                                                DateTimeOffset.UtcNow,
                                        CorrelationId = "test"
                                },
                                new PlannerPlan
                                {
                                        Objective = "Test planning objective.",
                                        FilesToInspect = Array.Empty<string>(),
                                        CandidateFilesToModify =
                                                Array.Empty<string>(),
                                        Strategy = "Test planning strategy.",
                                        AcceptanceCriteria =
                                                Array.Empty<string>(),
                                        Risks = Array.Empty<string>(),
                                        ExpectedTests = Array.Empty<string>(),
                                        Assumptions = Array.Empty<string>(),
                                        Uncertainties = Array.Empty<string>()
                                });

                public Task<PlanningExecutionResult> ExecuteAsync(
                        PlanningExecutionRequest request,
                        CancellationToken cancellationToken = default)
                {
                        CallCount++;
                        return Task.FromResult(Result);
                }
        }

        private sealed class FakeDeveloperExecutionService : IDeveloperExecutionService
        {
                public int CallCount { get; private set; }
                public DeveloperExecutionFailureKind FailureKind { get; set; }
                public DeveloperExecutionRequest? LastRequest { get; private set; }

                public Task<DeveloperExecutionResult> ExecuteAsync(DeveloperExecutionRequest request, CancellationToken cancellationToken = default)
                {
                        CallCount++;
                        LastRequest = request;
                        if (FailureKind != DeveloperExecutionFailureKind.None)
                                return Task.FromResult(DeveloperExecutionResult.Failure(FailureKind, "TEST_DEVELOPER_FAILURE"));

                        var proposal = new ValidatedDeveloperProposal(
                                "Test proposal",
                                new[] { new ValidatedDeveloperChange(
                                        request.HumanReviewCorrection is null
                                                ? DeveloperChangeOperationType.CreateFile
                                                : DeveloperChangeOperationType.ReplaceFile,
                                        "src/generated.cs", "Test", "class Generated {}",
                                        request.HumanReviewCorrection is null
                                                ? string.Empty
                                                : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                                                        System.Text.Encoding.UTF8.GetBytes(
                                                                request.HumanReviewCorrection.CurrentProposal.Changes[0].Content)))
                                                        .ToLowerInvariant(),
                                        18) },
                                Array.Empty<string>(), Array.Empty<string>(), 18, 100);
                        ArtifactRecord Artifact(ArtifactType type, string path) => new()
                        {
                                ArtifactId = Guid.NewGuid(), JobId = request.JobId, RunId = request.RunId,
                                ArtifactType = type, RelativePath = path, Sha256 = new string('a', 64), SizeBytes = 1,
                                CreatedAtUtc = DateTimeOffset.UtcNow, CorrelationId = request.CorrelationId
                        };
                        return Task.FromResult(DeveloperExecutionResult.Success(
                                new DeveloperExecutionReport
                                {
                                        JobId = request.JobId, RunId = request.RunId, LogicalModel = "CodingQuality",
                                        Provider = "Fake", PhysicalModel = "fake", Duration = TimeSpan.Zero, TerminationReason = "Stop"
                                },
                                Artifact(ArtifactType.DeveloperResponse, "developer/response.json"),
                                Artifact(ArtifactType.DeveloperProposal, "developer/proposal.json"), proposal));
                }
        }

        private sealed class FakeObservedChangeEvidenceService : IObservedChangeEvidenceService
        {
                public int CallCount { get; private set; }
                public ObservedChangeEvidenceFailureKind FailureKind { get; set; }
                public ObservedChangeEvidenceRequest? LastRequest { get; private set; }
                public Task<ObservedChangeEvidenceResult> CaptureAsync(ObservedChangeEvidenceRequest request, CancellationToken cancellationToken=default)
                {
                        CallCount++;
                        LastRequest = request;
                        if (FailureKind != ObservedChangeEvidenceFailureKind.None)
                                return Task.FromResult(ObservedChangeEvidenceResult.Failure(
                                        FailureKind, "TEST_OBSERVED_CHANGE_FAILURE"));
                        var manifest = new ObservedChangeManifest(request.JobId, request.RunId, request.Repository.Head, []);
                        var artifact = new ArtifactRecord { ArtifactId=Guid.NewGuid(), JobId=request.JobId, RunId=request.RunId,
                                ArtifactType=ArtifactType.ObservedChangeManifest, RelativePath="changes/manifest.json", Sha256=new string('a',64),
                                SizeBytes=1, CreatedAtUtc=DateTimeOffset.UtcNow, CorrelationId=request.CorrelationId };
                        return Task.FromResult(ObservedChangeEvidenceResult.Success(manifest, artifact));
                }
        }

        private sealed class FakeReviewerExecutionService : IReviewerExecutionService
        {
                public int CallCount { get; private set; }
                public ReviewerExecutionRequest? LastRequest { get; private set; }
                public ReviewerReview Review { get; set; } = new()
                {
                        Decision=ReviewerDecision.Approved, Findings=[], RequiredCorrections=[],
                        RiskAssessment="low", Summary="ok"
                };
                public Task<ReviewerExecutionResult> ExecuteAsync(
                        ReviewerExecutionRequest request, CancellationToken cancellationToken=default)
                {
                        CallCount++;
                        LastRequest = request;
                        ArtifactRecord review = CreateArtifact(request, ArtifactType.ReviewerReview);
                        ArtifactRecord response = CreateArtifact(request, ArtifactType.ReviewerResponse);
                        return Task.FromResult(ReviewerExecutionResult.Success(Review, review, response));
                }
                private static ArtifactRecord CreateArtifact(ReviewerExecutionRequest request, ArtifactType type) => new()
                {
                        ArtifactId=Guid.NewGuid(), JobId=request.JobId, RunId=request.RunId,
                        ArtifactType=type, RelativePath="review/artifact.json", Sha256=new string('a',64),
                        SizeBytes=1, CreatedAtUtc=DateTimeOffset.UtcNow, CorrelationId=request.CorrelationId
                };
        }

        private sealed class FakeReviewerEffectiveSourceSnapshotService :
                IReviewerEffectiveSourceSnapshotService
        {
                public int CallCount { get; private set; }

                public Task<ReviewerEffectiveSourceSnapshotResult> CaptureAsync(
                        ReviewerEffectiveSourceSnapshotRequest request,
                        CancellationToken cancellationToken = default)
                {
                        CallCount++;
                        ReviewerEffectiveSourceFile[] files = request.Plan
                                .CandidateFilesToModify
                                .Select(path => new ReviewerEffectiveSourceFile(
                                        path, new string('a', 64), 0, string.Empty))
                                .ToArray();
                        return Task.FromResult(
                                ReviewerEffectiveSourceSnapshotResult.Success(
                                        new ReviewerEffectiveSourceSnapshot(
                                                request.JobId,
                                                request.RunId,
                                                request.EffectiveProposalLineage,
                                                files)));
                }
        }

        private sealed class FakeReviewDecisionPolicy : IReviewDecisionPolicy
        {
                public int CallCount { get; private set; }
                public ReviewDecisionResult Evaluate(ReviewDecisionInput input)
                {
                        CallCount++;
                        return ReviewDecisionResult.Success(input.Review.Decision);
                }
        }

        private sealed class FakeSafeChangeApplier : ISafeChangeApplier
        {
                public int CallCount { get; private set; }
                public SafeChangeApplicationFailureKind FailureKind { get; set; }
                public SafeChangeApplicationRequest? LastRequest { get; private set; }

                public Task<SafeChangeApplicationResult> ApplyAsync(SafeChangeApplicationRequest request, CancellationToken cancellationToken = default)
                {
                        CallCount++;
                        LastRequest = request;
                        if (FailureKind != SafeChangeApplicationFailureKind.None)
                                return Task.FromResult(SafeChangeApplicationResult.Failure(FailureKind, "TEST_SAFE_CHANGE_FAILURE"));

                        DateTime now = DateTime.UtcNow;
                        return Task.FromResult(SafeChangeApplicationResult.Success(
                                new SafeChangeApplicationReport(request.JobId, request.RunId,
                                        new[] { new AppliedFileChange(DeveloperChangeOperationType.CreateFile, "src/generated.cs", null, new string('b', 64), 18) },
                                        18, now, now)));
                }
        }

        [Theory]
        [InlineData(DevelopmentChangeClassification.ArchitectureConflict)]
        [InlineData(DevelopmentChangeClassification.Unknown)]
        public async Task ContextBuilding_BlockedAnalysis_StopsBeforePlanning(
                DevelopmentChangeClassification classification)
        {
                Fixture fixture = CreateFixture(developmentClassification: classification);
                Result<Job> created = await fixture.Service.CreateAsync("request", "KRX-ANALYSIS-BLOCK");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "created");

                JobOperationResult result = await fixture.Orchestrator.AdvanceAsync(
                        created.Value.Id, "orchestrator", "analysis");

                Assert.True(result.IsSuccess);
                Assert.Equal(JobState.WaitingHuman, created.Value.State);
                Assert.Equal(0, fixture.Planning.CallCount);
                Assert.Equal(0, fixture.Developer.CallCount);
                Assert.Equal(0, fixture.SafeChange.CallCount);
        }

        [Fact]
        public async Task ContextBuilding_AlreadySatisfied_CompletesBeforePlanning()
        {
                Fixture fixture = CreateFixture(developmentClassification: DevelopmentChangeClassification.AlreadySatisfied);
                Result<Job> created = await fixture.Service.CreateAsync("request", "KRX-ANALYSIS-DONE");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "created");

                JobOperationResult result = await fixture.Orchestrator.AdvanceAsync(
                        created.Value.Id, "orchestrator", "analysis");

                Assert.True(result.IsSuccess);
                Assert.Equal(JobState.Completed, created.Value.State);
                Assert.Equal(0, fixture.Planning.CallCount);
                Assert.Equal(0, fixture.Developer.CallCount);
                Assert.Equal(0, fixture.SafeChange.CallCount);
        }

        [Theory]
        [InlineData(ArchitectureDecisionKind.PreserveExistingArchitecture)]
        [InlineData(ArchitectureDecisionKind.SupersedeRequest)]
        [InlineData(ArchitectureDecisionKind.NoCodeChangeRequired)]
        public async Task ArchitectureDecision_TerminatingNoCodeDecision_Completes(
                ArchitectureDecisionKind decision)
        {
                (Fixture fixture, Job job) = await ArchitectureDecisionJob();
                ArchitectureDecisionRequest request = DecisionRequest(decision);

                JobOperationResult result = await fixture.Orchestrator
                        .ResolveArchitectureDecisionAsync(job.Id, request);

                Assert.True(result.IsSuccess);
                Assert.Equal(JobState.Completed, job.State);
                ArtifactWriteRequest write = Assert.Single(fixture.Artifacts.Writes,
                        value => value.ArtifactType == ArtifactType.ArchitectureDecision);
                ArchitectureDecisionEvidence evidence = JsonSerializer.Deserialize<ArchitectureDecisionEvidence>(write.Content.Span)!;
                Assert.Equal(decision, evidence.Decision);
                Assert.False(evidence.SourceMutation);
                Assert.Equal(DevelopmentChangeClassification.ArchitectureConflict, evidence.Classification);
                Assert.Equal(0, fixture.Planning.CallCount);
                Assert.Equal(0, fixture.Developer.CallCount);
                Assert.Equal(0, fixture.SafeChange.CallCount);
                Assert.DoesNotContain(fixture.Artifacts.Writes, value => value.ArtifactType is
                        ArtifactType.BuildReport or ArtifactType.TestReport or ArtifactType.ReviewerReview);
        }

        [Fact]
        public async Task ArchitectureDecision_SamePayloadIsIdempotent_DifferentPayloadFailsClosed()
        {
                (Fixture fixture, Job job) = await ArchitectureDecisionJob();
                ArchitectureDecisionRequest request = DecisionRequest(ArchitectureDecisionKind.PreserveExistingArchitecture);

                Assert.True((await fixture.Orchestrator.ResolveArchitectureDecisionAsync(job.Id, request)).IsSuccess);
                Assert.True((await fixture.Orchestrator.ResolveArchitectureDecisionAsync(job.Id, request)).IsSuccess);
                Assert.Single(fixture.Artifacts.Writes,
                        value => value.ArtifactType == ArtifactType.ArchitectureDecision);

                JobOperationResult conflict = await fixture.Orchestrator.ResolveArchitectureDecisionAsync(
                        job.Id, request with { Reason = "Different reason." });
                Assert.False(conflict.IsSuccess);
                Assert.Equal(JobState.Completed, job.State);
        }

        [Fact]
        public async Task ArchitectureDecision_NonWaitingHumanFailsClosed()
        {
                Fixture fixture = CreateFixture();
                Result<Job> created = await fixture.Service.CreateAsync("request", "KRX-ARCH-NONWAITING");

                JobOperationResult result = await fixture.Orchestrator.ResolveArchitectureDecisionAsync(
                        created.Value.Id, DecisionRequest(ArchitectureDecisionKind.NoCodeChangeRequired));

                Assert.False(result.IsSuccess);
                Assert.Equal(JobState.Created, created.Value.State);
                Assert.DoesNotContain(fixture.Artifacts.Writes,
                        value => value.ArtifactType == ArtifactType.ArchitectureDecision);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public async Task ArchitectureDecision_IneligibleAnalysisFailsClosed(
                bool decisionRequired,
                bool developerAllowed)
        {
                (Fixture fixture, Job job) = await ArchitectureDecisionJob();
                ConfigureArchitectureRecovery(fixture, job, decisionRequired, developerAllowed);

                JobOperationResult result = await fixture.Orchestrator.ResolveArchitectureDecisionAsync(
                        job.Id, DecisionRequest(ArchitectureDecisionKind.NoCodeChangeRequired));

                Assert.False(result.IsSuccess);
                Assert.Equal(JobState.WaitingHuman, job.State);
        }

        [Fact]
        public async Task ArchitectureDecision_MismatchedAnalysisIdentityFailsClosed()
        {
                (Fixture fixture, Job job) = await ArchitectureDecisionJob();
                ConfigureArchitectureRecovery(fixture, job, true, false, analysisJobId: Guid.NewGuid());

                JobOperationResult result = await fixture.Orchestrator.ResolveArchitectureDecisionAsync(
                        job.Id, DecisionRequest(ArchitectureDecisionKind.PreserveExistingArchitecture));

                Assert.False(result.IsSuccess);
                Assert.Equal(JobState.WaitingHuman, job.State);
        }

        [Fact]
        public async Task ArchitectureDecision_AuthorizeScopeExpansionFailsClosed()
        {
                (Fixture fixture, Job job) = await ArchitectureDecisionJob();
                JobOperationResult result = await fixture.Orchestrator.ResolveArchitectureDecisionAsync(
                        job.Id, DecisionRequest(ArchitectureDecisionKind.AuthorizeScopeExpansion));
                Assert.False(result.IsSuccess);
                Assert.Equal(JobState.WaitingHuman, job.State);
        }

        private static async Task<(Fixture Fixture, Job Job)> ArchitectureDecisionJob()
        {
                Fixture fixture = CreateFixture(developmentClassification: DevelopmentChangeClassification.ArchitectureConflict);
                Result<Job> created = await fixture.Service.CreateAsync("request", $"KRX-ARCH-{Guid.NewGuid():N}"[..15]);
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "test", "created");
                await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "test", "analysis");
                Assert.Equal(JobState.WaitingHuman, created.Value.State);
                ConfigureArchitectureRecovery(fixture, created.Value, true, false);
                return (fixture, created.Value);
        }

        private static void ConfigureArchitectureRecovery(
                Fixture fixture,
                Job job,
                bool decisionRequired,
                bool developerAllowed,
                Guid? analysisJobId = null)
        {
                Guid runId = new DeterministicJobRunIdProvider().Create(job.Id, job.AttemptCount);
                fixture.RecoveryEvidence.Handler = request =>
                {
                        if (request.Stage == RecoveryStage.ArchitectureDecision)
                        {
                                ArtifactWriteRequest? write = fixture.Artifacts.Writes.LastOrDefault(value =>
                                        value.ArtifactType == ArtifactType.ArchitectureDecision &&
                                        value.CorrelationId == request.CorrelationId);
                                return write is null
                                        ? StageRecoveryResult.NotCompleted()
                                        : StageRecoveryResult.Completed(architectureDecision:
                                                JsonSerializer.Deserialize<ArchitectureDecisionEvidence>(write.Content.Span));
                        }
                        if (request.Stage != RecoveryStage.DevelopmentAnalysis)
                                return StageRecoveryResult.NotCompleted();
                        var analysis = new DevelopmentAnalysis
                        {
                                JobId = analysisJobId ?? job.Id, RunId = runId, AttemptCount = job.AttemptCount,
                                RequestIdentity = "request", TargetSymbols = ["Target"],
                                ExistingDeclarations = ["Target@src/Kronxy.Domain/Target.cs"],
                                PrimaryClassification = DevelopmentChangeClassification.ArchitectureConflict,
                                ImpactedLayers = ["Domain", "Application"], RequestedScope = "Domain-only",
                                RequiredScope = "Cross-layer", ScopeCompatible = false, BreakingContracts = [],
                                ArchitectureDecisionRequired = decisionRequired,
                                DeveloperExecutionAllowed = developerAllowed, Evidence = [], FilesInspected = [],
                                AnalysisVersion = "test"
                        };
                        return StageRecoveryResult.Completed(
                                developmentAnalysis: analysis,
                                developmentAnalysisArtifact: new ArtifactRecord
                                {
                                        ArtifactId = Guid.NewGuid(), JobId = job.Id, RunId = runId,
                                        ArtifactType = ArtifactType.DevelopmentAnalysis,
                                        RelativePath = "development-analysis/analysis.json",
                                        Sha256 = new string('a', 64), SizeBytes = 1,
                                        CreatedAtUtc = DateTimeOffset.UtcNow, CorrelationId = "analysis"
                                });
                };
        }

        private static ArchitectureDecisionRequest DecisionRequest(ArchitectureDecisionKind decision) => new()
        {
                Actor = "architect", CorrelationId = "architecture-decision-test",
                Decision = decision, Reason = "Preserve the existing architecture.",
                FollowUpRequired = true, FollowUpDescription = "SEPARATE_ARCHITECTURE_DECISION"
        };

	private static Fixture CreateFixture(int maxAttempts = 3, TimeSpan? maxDuration = null,
                DevelopmentChangeClassification? developmentClassification = null)
	{
		FakeJobRepository fakeJobRepository = new FakeJobRepository();
		FakeUnitOfWork unitOfWork = new FakeUnitOfWork();
		FakeClock clock = new FakeClock(UtcNow);
		FakeJobIdGenerator idGenerator = new FakeJobIdGenerator();
		JobControlOptions options = new JobControlOptions
		{
			MaxJobDuration = (maxDuration ?? TimeSpan.FromHours(2.0)),
			MaxAttempts = maxAttempts,
			MaxAgentIterations = 10,
			MaxAiCalls = 20
		};
		JobService service = new JobService(fakeJobRepository, unitOfWork, clock, idGenerator, options);
		JobStateMachine stateMachine = new JobStateMachine();
		FakeSourceRevisionProvider sourceRevision =
		        new FakeSourceRevisionProvider();

		FakeExecutionPlaneLifecycle executionPlane =
		        new FakeExecutionPlaneLifecycle();


                FakeContextGenerationService context =
                        new FakeContextGenerationService();

                FakeRestoreExecutionService restore =
                        new FakeRestoreExecutionService();

                FakeExecutionTargetProvider target =
                        new FakeExecutionTargetProvider();

                FakeBuildExecutionService build =
                        new FakeBuildExecutionService();

                FakeTestExecutionService test =
                        new FakeTestExecutionService();

                FakePlanningExecutionService planning =
                        new FakePlanningExecutionService();

                FakeDeveloperExecutionService developer =
                        new FakeDeveloperExecutionService();

                FakeSafeChangeApplier safeChange =
                        new FakeSafeChangeApplier();

                FakeStageRecoveryEvidenceService recoveryEvidence =
                        new FakeStageRecoveryEvidenceService();

                FakeObservedChangeEvidenceService observed = new();
                FakeReviewerEffectiveSourceSnapshotService effectiveSource = new();
                FakeReviewerExecutionService reviewer = new();
                FakeReviewDecisionPolicy reviewPolicy = new();
                FakeArtifactStore artifacts = new();
                FakeArtifactMetadataRepository artifactMetadata = new();

                IJobRunIdProvider runIdProvider =
                        new DeterministicJobRunIdProvider();

		JobOrchestrator orchestrator =
		        new JobOrchestrator(
		                fakeJobRepository,
		                unitOfWork,
		                clock,
		                stateMachine,
		                sourceRevision,
		                executionPlane,
                                runIdProvider,
                                context,
                                target,
                                restore,
                                build,
                                test,
                                planning,
                                developer,
                                safeChange,
                                observed,
                                recoveryEvidence,
                                effectiveSource,
                                reviewer,
                                reviewPolicy,
                                artifacts,
                                artifactMetadata,
                                developmentAnalysisService: developmentClassification is null
                                        ? null
                                        : new FakeDevelopmentAnalysisService(developmentClassification.Value));

		return new Fixture(
		        fakeJobRepository,
		        unitOfWork,
		        clock,
		        service,
		        orchestrator,
		        sourceRevision,
		        executionPlane,
                        context,
                        restore,
                        target,
                        build,
                        test,
                        planning,
                        developer,
                        safeChange,
                        observed,
                        recoveryEvidence,
                        reviewer,
                        reviewPolicy,
                        artifacts,
                        artifactMetadata);
	}
}
