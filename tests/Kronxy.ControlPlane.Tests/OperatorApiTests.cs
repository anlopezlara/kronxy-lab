using Kronxy.Api.Controllers;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Execution;
using Kronxy.Application.Jobs;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Jobs;
using Kronxy.Infrastructure.Artifacts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class OperatorApiTests
{
    [Fact]
    public async Task Real_http_smoke_has_no_unexpected_500_and_exposes_no_host_paths()
    {
        Job job = NewJob("OP-SMOKE");
        await using WebApplication app = await StartHttpAsync(job);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        string[] routes = ["health", "api/jobs", $"api/jobs/{job.Id}",
            $"api/jobs/{job.Id}/history", $"api/jobs/{job.Id}/artifacts",
            $"api/jobs/{job.Id}/effective-lineage", $"api/jobs/{job.Id}/allowed-actions"];
        foreach (string route in routes)
        {
            HttpResponseMessage response = await client.GetAsync(route);
            Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.True(response.IsSuccessStatusCode);
            Assert.DoesNotContain("/home/", await response.Content.ReadAsStringAsync());
        }
        await app.StopAsync();
    }
    [Fact]
    public async Task Health_returns_200_when_database_is_usable()
    {
        IActionResult result = await new HealthController().Get(new Health(true), default);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Health_returns_503_when_database_is_unavailable()
    {
        ObjectResult result = Assert.IsType<ObjectResult>(
            await new HealthController().Get(new Health(false), default));
        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public async Task Job_list_is_bounded_and_carries_filters_and_run_id()
    {
        Job job = NewJob("OP-1");
        var repository = new Jobs(job);
        OperatorJobPage page = await Service(repository).GetJobsAsync(
            1, 1000, "OP-1", JobState.Created);

        Assert.Equal(100, page.PageSize);
        Assert.Equal("OP-1", repository.ExternalId);
        Assert.Equal(JobState.Created, repository.State);
        Assert.NotEqual(Guid.Empty, Assert.Single(page.Items).RunId);
    }

    [Fact]
    public async Task History_is_ordered_and_contains_governed_fields()
    {
        Job job = NewJob("OP-HISTORY");
        Assert.True(job.TransitionTo(JobState.ContextBuilding, DateTime.UtcNow.AddSeconds(1),
            "start", "operator", "one").IsSuccess);
        Assert.True(job.TransitionTo(JobState.Planning, DateTime.UtcNow.AddSeconds(2),
            "next", "operator", "two").IsSuccess);

        var history = await Service(new Jobs(job)).GetHistoryAsync(job.Id);
        IReadOnlyList<OperatorHistoryItem> actual = Assert.IsAssignableFrom<IReadOnlyList<OperatorHistoryItem>>(history);
        Assert.Equal([1, 2], actual.Select(item => item.Sequence));
        Assert.True(actual[0].Timestamp < actual[1].Timestamp);
        Assert.Equal("one", actual[0].CorrelationId);
    }

    [Fact]
    public async Task Artifact_list_uses_opaque_api_identity_and_never_host_path()
    {
        Job job = NewJob("OP-ARTIFACT");
        Guid runId = new DeterministicJobRunIdProvider().Create(job.Id, 1);
        var metadata = new Metadata(new ArtifactRecord
        {
            ArtifactId = Guid.NewGuid(), JobId = job.Id, RunId = runId,
            ArtifactType = ArtifactType.BuildReport,
            RelativePath = "/home/andres/secret/report.json", Sha256 = new string('a', 64),
            SizeBytes = 10, CreatedAtUtc = DateTimeOffset.UtcNow, CorrelationId = "build"
        });
        var result = await Service(new Jobs(job), metadata).GetArtifactsAsync(job.Id);
        OperatorArtifactItem item = Assert.Single(result!);
        Assert.StartsWith($"/api/jobs/{job.Id}/artifacts/", item.ApiPath);
        Assert.DoesNotContain("/home/", item.ApiPath);
    }

    [Fact]
    public async Task Artifact_cross_job_access_is_rejected()
    {
        Job job = NewJob("OP-CROSS");
        var reader = new Reader();
        OperatorArtifactContent? result = await Service(new Jobs(job), reader: reader)
            .GetArtifactAsync(job.Id, Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task Artifact_reader_rejects_path_traversal_metadata()
    {
        string root = Path.Combine(Path.GetTempPath(), $"kronxy-operator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            Job job = NewJob("OP-TRAVERSAL");
            Guid runId = new DeterministicJobRunIdProvider().Create(job.Id, 1);
            ArtifactRecord artifact = Artifact(job.Id, runId, ArtifactType.BuildReport, DateTimeOffset.UtcNow)
                with { RelativePath = "../outside.json" };
            var reader = new FileSystemArtifactReader(
                new ArtifactStoreOptions { RootPath = root }, new Metadata(artifact));
            ArtifactReadResult result = await reader.ReadByIdAsync(artifact.ArtifactId, job.Id, runId);
            Assert.False(result.IsSuccess);
            Assert.Equal(ArtifactReadFailureKind.UnsafePath, result.FailureKind);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Terminal_job_exposes_no_mutation_actions()
    {
        Job job = NewJob("OP-CANCELLED");
        Assert.True(job.TransitionTo(JobState.Cancelled, DateTime.UtcNow, "cancel", "operator", "cancel").IsSuccess);
        var actions = await Service(new Jobs(job)).GetAllowedActionsAsync(job.Id);
        Assert.All(actions!, action => Assert.False(action.Allowed));
    }

    [Theory]
    [InlineData(JobState.Created, "advance")]
    [InlineData(JobState.Planning, "advance")]
    [InlineData(JobState.Building, "advance")]
    [InlineData(JobState.WaitingHuman, "humanReviewApprove")]
    [InlineData(JobState.RetryPending, "resume")]
    public async Task Allowed_actions_are_derived_from_server_workflow(JobState state, string expected)
    {
        Job job = JobInState(state);
        var actions = await Service(new Jobs(job)).GetAllowedActionsAsync(job.Id);
        Assert.True(actions!.Single(action => action.Action == expected).Allowed);
    }

    [Theory]
    [InlineData(ArtifactType.DeveloperProposal, "Developer")]
    [InlineData(ArtifactType.DeveloperBuildCorrectionProposal, "BuildCorrection")]
    [InlineData(ArtifactType.GovernedHumanCorrectionReceipt, "GovernedHumanCorrection")]
    public async Task Effective_lineage_uses_authoritative_artifact_precedence(
        ArtifactType sourceType, string expected)
    {
        Job job = NewJob($"OP-{expected}");
        Guid runId = new DeterministicJobRunIdProvider().Create(job.Id, 1);
        ArtifactRecord source = Artifact(job.Id, runId, sourceType, DateTimeOffset.UtcNow);
        OperatorLineage? lineage = await Service(new Jobs(job), new Metadata(source))
            .GetEffectiveLineageAsync(job.Id);
        Assert.Equal(expected, lineage!.LineageType);
    }

    [Fact]
    public async Task Ambiguous_lineage_fails_closed()
    {
        Job job = NewJob("OP-AMBIGUOUS");
        Guid runId = new DeterministicJobRunIdProvider().Create(job.Id, 1);
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        OperatorLineage? lineage = await Service(new Jobs(job), new Metadata(
            Artifact(job.Id, runId, ArtifactType.DeveloperProposal, timestamp),
            Artifact(job.Id, runId, ArtifactType.DeveloperProposal, timestamp)))
            .GetEffectiveLineageAsync(job.Id);
        Assert.Null(lineage);
    }

    private static OperatorJobService Service(Jobs jobs, Metadata? metadata = null, Reader? reader = null) =>
        new(jobs, new DeterministicJobRunIdProvider(), metadata ?? new Metadata(), reader ?? new Reader());

    private static Job NewJob(string externalId) => Job.Create(Guid.NewGuid(), externalId,
        "operator request", JobLimits.Create(TimeSpan.FromHours(1), 3, 3, 3).Value,
        DateTime.UtcNow).Value;

    private static Job JobInState(JobState target)
    {
        Job job = NewJob($"OP-{target}");
        if (target == JobState.Created) return job;
        JobState[] path = [JobState.ContextBuilding, JobState.Planning, JobState.WorkspacePreparing,
            JobState.Developing, JobState.Building, JobState.Testing, JobState.Reviewing, JobState.WaitingHuman];
        foreach (JobState state in path)
        {
            Assert.True(job.TransitionTo(state, DateTime.UtcNow, "test", "test", Guid.NewGuid().ToString()).IsSuccess);
            if (state == target) return job;
            if (target == JobState.RetryPending && state == JobState.Planning)
            {
                Assert.True(job.TransitionTo(target, DateTime.UtcNow, "retry", "test", "retry").IsSuccess);
                return job;
            }
        }
        if (target == JobState.Completed)
            Assert.True(job.TransitionTo(target, DateTime.UtcNow, "complete", "test", "complete").IsSuccess);
        return job;
    }

    private static ArtifactRecord Artifact(Guid jobId, Guid runId, ArtifactType type,
        DateTimeOffset timestamp) => new()
    {
        ArtifactId = Guid.NewGuid(), JobId = jobId, RunId = runId, ArtifactType = type,
        RelativePath = $"{jobId:N}/{runId:N}/{Guid.NewGuid():N}.json", Sha256 = new string('a', 64),
        SizeBytes = 2, CreatedAtUtc = timestamp, CorrelationId = "operator"
    };

    private static async Task<WebApplication> StartHttpAsync(Job job)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(HealthController).Assembly);
        builder.Services.AddSingleton<IOperatorHealthService>(new Health(true));
        builder.Services.AddSingleton<IOperatorJobService>(new SmokeOperator(job));
        builder.Services.AddSingleton<IJobService>(new SmokeJobs(job));
        builder.Services.AddSingleton<IJobOrchestrator, SmokeOrchestrator>();
        WebApplication app = builder.Build();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private sealed class SmokeOperator(Job job) : IOperatorJobService
    {
        public Task<OperatorJobPage> GetJobsAsync(int page, int pageSize, string? externalId, JobState? state, CancellationToken token = default) =>
            Task.FromResult(new OperatorJobPage([], page, pageSize, 0, 0));
        public Task<Job?> GetJobAsync(Guid id, CancellationToken token = default) => Task.FromResult<Job?>(job);
        public Task<IReadOnlyList<OperatorHistoryItem>?> GetHistoryAsync(Guid id, CancellationToken token = default) => Task.FromResult<IReadOnlyList<OperatorHistoryItem>?>([]);
        public Task<IReadOnlyList<OperatorArtifactItem>?> GetArtifactsAsync(Guid id, CancellationToken token = default) => Task.FromResult<IReadOnlyList<OperatorArtifactItem>?>([]);
        public Task<OperatorArtifactContent?> GetArtifactAsync(Guid id, Guid artifactId, CancellationToken token = default) => Task.FromResult<OperatorArtifactContent?>(null);
        public Task<OperatorLineage?> GetEffectiveLineageAsync(Guid id, CancellationToken token = default) => Task.FromResult<OperatorLineage?>(new("Developer", "smoke", Guid.NewGuid(), 1, true, true, "Available", "Available", "Available"));
        public Task<IReadOnlyList<OperatorAllowedAction>?> GetAllowedActionsAsync(Guid id, CancellationToken token = default) => Task.FromResult<IReadOnlyList<OperatorAllowedAction>?>([new("advance", true, null)]);
    }

    private sealed class SmokeJobs(Job job) : IJobService
    {
        public Task<Result<Job>> CreateAsync(string request, string? externalId = null, CancellationToken token = default) => Task.FromResult<Result<Job>>(job);
        public Task<Result<Job>> GetAsync(Guid id, CancellationToken token = default) => Task.FromResult<Result<Job>>(job);
        public Task<JobOperationResult> CancelAsync(Guid id, string actor, string correlationId, CancellationToken token = default) => Task.FromResult(JobOperationResult.Success());
    }

    private sealed class SmokeOrchestrator : IJobOrchestrator
    {
        private static Task<JobOperationResult> Ok() => Task.FromResult(JobOperationResult.Success());
        public Task<JobOperationResult> AdvanceAsync(Guid id, string actor, string correlationId, CancellationToken token = default) => Ok();
        public Task<JobOperationResult> MarkRetryPendingAsync(Guid id, string reason, string actor, string correlationId, CancellationToken token = default) => Ok();
        public Task<JobOperationResult> ResumeAsync(Guid id, string reason, string actor, string correlationId, CancellationToken token = default) => Ok();
        public Task<JobOperationResult> RequestHumanReviewCorrectionAsync(Guid id, HumanReviewCorrectionRequest request, CancellationToken token = default) => Ok();
        public Task<JobOperationResult> ApplyGovernedHumanCorrectionAsync(Guid id, GovernedHumanCorrectionRequest request, CancellationToken token = default) => Ok();
        public Task<JobOperationResult> SupersedeHumanReviewCorrectionReviewAsync(Guid id, string actor, string correlationId, CancellationToken token = default) => Ok();
        public Task<JobOperationResult> ApproveHumanReviewAsync(Guid id, string actor, string correlationId, CancellationToken token = default) => Ok();
        public Task<JobOperationResult> ResolveArchitectureDecisionAsync(Guid id, ArchitectureDecisionRequest request, CancellationToken token = default) => Ok();
        public JobState? DetermineNextState(JobState currentState) => JobWorkflow.DetermineNextAutomaticState(currentState);
    }

    private sealed class Health(bool usable) : IOperatorHealthService
    {
        public Task<OperatorHealth> CheckAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new OperatorHealth(usable));
    }

    private sealed class Jobs(params Job[] values) : IJobRepository
    {
        public string? ExternalId { get; private set; }
        public JobState? State { get; private set; }
        public void Add(Job job) { }
        public Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(values.SingleOrDefault(job => job.Id == id));
        public Task<Job?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default) =>
            Task.FromResult(values.SingleOrDefault(job => job.ExternalId == externalId));
        public Task<(IReadOnlyList<Job> Items, int TotalItems)> GetPageAsync(int page, int pageSize,
            string? externalId, JobState? state, CancellationToken cancellationToken = default)
        {
            ExternalId = externalId; State = state;
            IReadOnlyList<Job> filtered = values.Where(job =>
                (externalId is null || job.ExternalId == externalId) &&
                (!state.HasValue || job.State == state)).Take(pageSize).ToArray();
            return Task.FromResult((filtered, filtered.Count));
        }
    }

    private sealed class Metadata(params ArtifactRecord[] values) : IArtifactMetadataRepository
    {
        public Task AddAsync(ArtifactRecord artifact, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ArtifactRecord?> GetByIdAsync(Guid artifactId, CancellationToken cancellationToken = default) =>
            Task.FromResult(values.SingleOrDefault(item => item.ArtifactId == artifactId));
        public Task<IReadOnlyList<ArtifactRecord>> GetByJobAndRunAsync(Guid jobId, Guid runId,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ArtifactRecord>>(
                values.Where(item => item.JobId == jobId && item.RunId == runId).ToArray());
    }

    private sealed class Reader : IArtifactReader
    {
        public Task<ArtifactReadResult> ReadAsync(ArtifactReadRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(
                ArtifactReadResult.Failure(ArtifactReadFailureKind.NotFound, "NOT_FOUND"));
    }
}
