using System.Net;
using System.Net.Http.Json;
using System.Text;
using Kronxy.Api.Controllers.Jobs;
using Kronxy.Application.Execution;
using Kronxy.Application.Jobs;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Jobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class JobRequestHttpValidationTests
{
    private static readonly Guid NormalJobId =
        Guid.Parse("10000000-0000-4000-8000-000000000001");

    private static readonly Guid UnknownJobId =
        Guid.Parse("10000000-0000-4000-8000-000000000002");

    private static readonly Guid CancelledJobId =
        Guid.Parse("10000000-0000-4000-8000-000000000003");

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"actor\":\"tester\",\"correlationId\":\"\"}")]
    [InlineData("{\"actor\":\"tester\",\"correlationId\":\"   \"}")]
    public async Task JobActionRequest_rejects_invalid_correlation_id(
        string json)
    {
        await using HttpHost host = await HttpHost.StartAsync();

        HttpResponseMessage response = await host.Client.PostAsync(
            $"api/jobs/{NormalJobId}/advance",
            Json(json));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("{\"reason\":\"test\",\"actor\":\"tester\"}")]
    [InlineData("{\"reason\":\"test\",\"actor\":\"tester\",\"correlationId\":\"\"}")]
    [InlineData("{\"reason\":\"test\",\"actor\":\"tester\",\"correlationId\":\"   \"}")]
    public async Task JobReasonActionRequest_rejects_invalid_correlation_id(
        string json)
    {
        await using HttpHost host = await HttpHost.StartAsync();

        HttpResponseMessage response = await host.Client.PostAsync(
            $"api/jobs/{NormalJobId}/resume",
            Json(json));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Valid_requests_reach_normal_application_behavior()
    {
        await using HttpHost host = await HttpHost.StartAsync();

        HttpResponseMessage action = await host.Client.PostAsJsonAsync(
            $"api/jobs/{NormalJobId}/advance",
            new { actor = "tester", correlationId = "valid-action" });

        HttpResponseMessage reason = await host.Client.PostAsJsonAsync(
            $"api/jobs/{NormalJobId}/resume",
            new
            {
                reason = "test",
                actor = "tester",
                correlationId = "valid-reason"
            });

        Assert.Equal(HttpStatusCode.OK, action.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reason.StatusCode);
    }

    [Fact]
    public async Task Unknown_job_with_valid_request_returns_governed_4xx()
    {
        await using HttpHost host = await HttpHost.StartAsync();

        HttpResponseMessage response = await host.Client.PostAsJsonAsync(
            $"api/jobs/{UnknownJobId}/resume",
            ValidReasonRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cancelled_job_with_valid_request_returns_conflict()
    {
        await using HttpHost host = await HttpHost.StartAsync();

        HttpResponseMessage response = await host.Client.PostAsJsonAsync(
            $"api/jobs/{CancelledJobId}/resume",
            ValidReasonRequest());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Malformed_json_returns_problem_details()
    {
        await using HttpHost host = await HttpHost.StartAsync();

        HttpResponseMessage response = await host.Client.PostAsync(
            $"api/jobs/{NormalJobId}/resume",
            Json("{\"reason\":"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Cancel_endpoint_still_reaches_application_behavior()
    {
        await using HttpHost host = await HttpHost.StartAsync();

        HttpResponseMessage response = await host.Client.PostAsJsonAsync(
            $"api/jobs/{NormalJobId}/cancel",
            new { actor = "tester", correlationId = "valid-cancel" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static object ValidReasonRequest() => new
    {
        reason = "test",
        actor = "tester",
        correlationId = "valid-correlation"
    };

    private static StringContent Json(string value) =>
        new(value, Encoding.UTF8, "application/json");

    private sealed class HttpHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private HttpHost(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<HttpHost> StartAsync()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services
                .AddControllers()
                .AddApplicationPart(typeof(JobsController).Assembly);
            builder.Services.AddSingleton<IJobService, FakeJobService>();
            builder.Services.AddSingleton<IJobOrchestrator, FakeJobOrchestrator>();

            WebApplication app = builder.Build();
            app.MapControllers();
            await app.StartAsync();

            return new HttpHost(
                app,
                new HttpClient
                {
                    BaseAddress = new Uri(app.Urls.Single())
                });
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private sealed class FakeJobService : IJobService
    {
        public Task<Result<Job>> CreateAsync(
            string request,
            string? externalId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<Job>> GetAsync(
            Guid jobId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobOperationResult> CancelAsync(
            Guid jobId,
            string actor,
            string correlationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(JobOperationResult.Success());
    }

    private sealed class FakeJobOrchestrator : IJobOrchestrator
    {
        public Task<JobOperationResult> AdvanceAsync(
            Guid jobId, string actor, string correlationId,
            CancellationToken cancellationToken = default) => Respond(jobId);

        public Task<JobOperationResult> MarkRetryPendingAsync(
            Guid jobId, string reason, string actor, string correlationId,
            CancellationToken cancellationToken = default) => Respond(jobId);

        public Task<JobOperationResult> ResumeAsync(
            Guid jobId, string reason, string actor, string correlationId,
            CancellationToken cancellationToken = default) => Respond(jobId);

        public Task<JobOperationResult> RequestHumanReviewCorrectionAsync(
            Guid jobId, HumanReviewCorrectionRequest request,
            CancellationToken cancellationToken = default) => Respond(jobId);

        public Task<JobOperationResult> ApplyGovernedHumanCorrectionAsync(
            Guid jobId, GovernedHumanCorrectionRequest request,
            CancellationToken cancellationToken = default) => Respond(jobId);

        public Task<JobOperationResult> SupersedeHumanReviewCorrectionReviewAsync(
            Guid jobId, string actor, string correlationId,
            CancellationToken cancellationToken = default) => Respond(jobId);

        public Task<JobOperationResult> ApproveHumanReviewAsync(
            Guid jobId, string actor, string correlationId,
            CancellationToken cancellationToken = default) => Respond(jobId);

        public Task<JobOperationResult> ResolveArchitectureDecisionAsync(
            Guid jobId, ArchitectureDecisionRequest request,
            CancellationToken cancellationToken = default) => Respond(jobId);

        public JobState? DetermineNextState(JobState currentState) => null;

        private static Task<JobOperationResult> Respond(Guid jobId)
        {
            JobOperationResult result = jobId == UnknownJobId
                ? JobOperationResult.Failure(
                    JobOperationKind.PermanentFailure,
                    JobApplicationErrors.NotFound)
                : jobId == CancelledJobId
                    ? JobOperationResult.Failure(
                        JobOperationKind.InvalidTransition,
                        JobApplicationErrors.TerminalJob)
                    : JobOperationResult.Success();

            return Task.FromResult(result);
        }
    }
}
