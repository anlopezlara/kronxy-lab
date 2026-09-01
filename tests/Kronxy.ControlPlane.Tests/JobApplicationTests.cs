using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
                FakeStageRecoveryEvidenceService RecoveryEvidence);

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
		fixture.Clock.UtcNow = UtcNow.AddMinutes(31.0);
		JobOperationResult result = await fixture.Orchestrator.AdvanceAsync(created.Value.Id, "orchestrator", "corr-timeout");
		Assert.False(result.IsSuccess);
		Assert.Equal<JobOperationKind>(JobOperationKind.TimedOut, result.Kind);
		Assert.Equal<JobState>(JobState.TimedOut, created.Value.State);
		Assert.True(created.Value.IsTerminal);
		Assert.NotNull<DateTime>(created.Value.CompletedOnUtc);
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
                    saveCount,
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
                    saveCount,
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
	                saveCount,
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
	                saveCount,
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
                   saveCount,
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
                                });

                public Task<PlanningExecutionResult> ExecuteAsync(
                        PlanningExecutionRequest request,
                        CancellationToken cancellationToken = default)
                {
                        CallCount++;
                        return Task.FromResult(Result);
                }
        }

	private static Fixture CreateFixture(int maxAttempts = 3, TimeSpan? maxDuration = null)
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

                FakeStageRecoveryEvidenceService recoveryEvidence =
                        new FakeStageRecoveryEvidenceService();

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
                                recoveryEvidence);

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
                        recoveryEvidence);
	}
}
