using System.Net;
using System.Text;
using Kronxy.Web.Clients;
using Kronxy.Web.Models;
using Kronxy.Web.Presentation;
using Xunit;

namespace Kronxy.Web.Tests;

public sealed class KronxyApiClientTests
{
    [Fact]
    public async Task Jobs_query_is_server_paged_and_safely_encoded()
    {
        var handler = new RecordingHandler("""{"items":[],"page":2,"pageSize":25,"totalItems":0,"totalPages":0}""");
        var client = Client(handler);
        await client.GetJobsAsync(2, 25, "KRX 1&2", "WaitingHuman");
        Assert.Equal("/api/jobs?page=2&pageSize=25&externalId=KRX%201%262&state=WaitingHuman",
            handler.Requests.Single().RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task Client_generates_read_only_endpoint_uris()
    {
        Guid jobId = Guid.Parse("10000000-0000-4000-8000-000000000001");
        Guid artifactId = Guid.Parse("20000000-0000-4000-8000-000000000002");
        var handler = new RouteHandler(); var client = Client(handler);
        await client.GetHealthAsync(); await client.GetJobAsync(jobId); await client.GetJobHistoryAsync(jobId);
        await client.GetJobArtifactsAsync(jobId); await client.GetArtifactAsync(jobId, artifactId);
        await client.GetEffectiveLineageAsync(jobId); await client.GetAllowedActionsAsync(jobId);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
        Assert.DoesNotContain(handler.Requests, request => request.RequestUri!.AbsolutePath.Contains("advance") ||
            request.RequestUri.AbsolutePath.Contains("resume") || request.RequestUri.AbsolutePath.Contains("cancel"));
    }

    [Fact]
    public async Task Create_job_posts_contract_once()
    {
        var handler = new RecordingHandler(JobJson);
        JobDetailDto created = await Client(handler).CreateJobAsync("Implement UI", "KRX-009999");
        Assert.Equal("KRX-1", created.ExternalId);
        HttpRequestMessage request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/jobs", request.RequestUri!.AbsolutePath);
        Assert.Contains("Implement UI", Assert.Single(handler.Bodies));
    }

    [Theory]
    [InlineData("advance", "/api/jobs/10000000-0000-4000-8000-000000000001/advance")]
    [InlineData("cancel", "/api/jobs/10000000-0000-4000-8000-000000000001/cancel")]
    [InlineData("approve", "/api/jobs/10000000-0000-4000-8000-000000000001/human-review/approve")]
    [InlineData("supersede", "/api/jobs/10000000-0000-4000-8000-000000000001/reviewer/human-review-correction/supersede")]
    public async Task Simple_action_posts_expected_route_once(string action, string route)
    {
        var handler = new RecordingHandler("""{"kind":"Success"}""");
        var client = Client(handler); Guid jobId = Guid.Parse("10000000-0000-4000-8000-000000000001");
        _ = action switch
        {
            "advance" => await client.AdvanceJobAsync(jobId),
            "cancel" => await client.CancelJobAsync(jobId),
            "approve" => await client.ApproveHumanReviewAsync(jobId),
            _ => await client.SupersedeReviewerCorrectionAsync(jobId)
        };
        HttpRequestMessage request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method); Assert.Equal(route, request.RequestUri!.AbsolutePath);
    }

    [Theory]
    [InlineData("resume", "/api/jobs/10000000-0000-4000-8000-000000000001/resume")]
    [InlineData("retry", "/api/jobs/10000000-0000-4000-8000-000000000001/retry-pending")]
    public async Task Reason_action_posts_reason_and_expected_route(string action, string route)
    {
        var handler = new RecordingHandler("""{"kind":"Success"}"""); var client = Client(handler);
        Guid jobId = Guid.Parse("10000000-0000-4000-8000-000000000001");
        if (action == "resume") await client.ResumeJobAsync(jobId, "operator reason");
        else await client.RetryPendingAsync(jobId, "operator reason");
        Assert.Equal(route, Assert.Single(handler.Requests).RequestUri!.AbsolutePath);
        Assert.Contains("operator reason", Assert.Single(handler.Bodies));
    }

    [Fact]
    public async Task Architecture_decision_posts_numeric_public_contract()
    {
        var handler = new RecordingHandler("""{"kind":"Success"}"""); var client = Client(handler);
        await client.ResolveArchitectureDecisionAsync(Guid.Parse("10000000-0000-4000-8000-000000000001"),
            new(ArchitectureDecisionDto.PreserveExistingArchitecture, "Keep boundaries", false, null));
        string body = Assert.Single(handler.Bodies);
        Assert.Contains("\"decision\":0", body); Assert.Contains("Keep boundaries", body);
    }

    [Fact]
    public async Task Human_review_changes_posts_actual_correction_contract()
    {
        var handler = new RecordingHandler("""{"kind":"Success"}"""); var client = Client(handler);
        await client.RequestHumanReviewChangesAsync(Guid.Parse("10000000-0000-4000-8000-000000000001"),
            [new("src/Kronxy.Web/Program.cs", "Keep the HTTP boundary")]);
        HttpRequestMessage request = Assert.Single(handler.Requests);
        Assert.EndsWith("/human-review/changes-required", request.RequestUri!.AbsolutePath);
        string body = Assert.Single(handler.Bodies);
        Assert.Contains("requiredCorrections", body); Assert.Contains("relativePath", body); Assert.Contains("instruction", body);
    }

    [Fact]
    public async Task Human_correction_client_uses_governed_hash_contract()
    {
        var handler = new RecordingHandler("""{"kind":"Success"}"""); var client = Client(handler);
        await client.ApplyHumanCorrectionAsync(Guid.Parse("10000000-0000-4000-8000-000000000001"), "Governed fix",
            [new("src/a.cs", new string('a', 64), "new content")]);
        Assert.EndsWith("/human-correction", Assert.Single(handler.Requests).RequestUri!.AbsolutePath);
        string body = Assert.Single(handler.Bodies);
        Assert.Contains("expectedContentSha256", body); Assert.Contains("Governed fix", body);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "Validation error")]
    [InlineData(HttpStatusCode.Conflict, "Conflict")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "Service unavailable")]
    public async Task Governed_error_statuses_are_safe(HttpStatusCode status, string expected)
    {
        var client = Client(new RecordingHandler("not-json", status, "text/plain"));
        KronxyApiException error = await Assert.ThrowsAsync<KronxyApiException>(() => client.GetHealthAsync());
        Assert.Equal(expected, error.Message);
    }

    [Fact]
    public void Local_operator_generates_distinct_visible_correlations()
    {
        var identity = new TestIdentity();
        string first = identity.NewCorrelationId("advance"); string second = identity.NewCorrelationId("advance");
        Assert.NotEqual(first, second); Assert.StartsWith("web-advance-", first);
    }

    [Fact]
    public void Submission_guard_rejects_duplicate_until_operation_completes()
    {
        var guard = new OperatorActionGuard();
        Assert.True(guard.TryBegin()); Assert.True(guard.IsActive);
        Assert.False(guard.TryBegin());
        guard.Complete();
        Assert.True(guard.TryBegin());
    }

    [Theory]
    [InlineData("Completed", "success")]
    [InlineData("WaitingHuman", "warning")]
    [InlineData("Cancelled", "danger")]
    [InlineData("Building", "active")]
    public void Job_state_rendering_is_consistent(string state, string expected) =>
        Assert.Equal(expected, OperatorPresentation.StateClass(state));

    [Fact]
    public void Allowed_actions_render_only_server_allowed_values()
    {
        AllowedActionDto[] values = [new("advance", false, "NO"), new("humanReviewApprove", true, null)];
        AllowedActionDto visible = Assert.Single(OperatorPresentation.VisibleActions(values));
        Assert.Equal("Human Review Approve", OperatorPresentation.ActionLabel(visible.Action));
    }

    [Fact]
    public async Task Health_contract_renders_safe_status_fields()
    {
        var client = Client(new RecordingHandler("""{"status":"Healthy","api":"Healthy","database":"Healthy"}"""));
        HealthDto health = await client.GetHealthAsync();
        Assert.Equal(("Healthy", "Healthy", "Healthy"), (health.Status, health.Api, health.Database));
    }

    [Fact]
    public async Task Safe_error_maps_status_code_without_raw_body()
    {
        var client = Client(new RecordingHandler("database stack trace", HttpStatusCode.ServiceUnavailable, "text/plain"));
        KronxyApiException error = await Assert.ThrowsAsync<KronxyApiException>(() => client.GetHealthAsync());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.Equal("HTTP_503", error.Code);
        Assert.DoesNotContain("stack trace", error.Message);
    }

    [Fact]
    public async Task Governed_error_preserves_high_level_and_specific_diagnostic_codes()
    {
        var client = Client(new RecordingHandler(
            """{"code":"Job.PlanningExecutionFailed","message":"The AI planning execution did not complete successfully.","diagnosticCode":"PLANNING_PATH_COHERENCE_INVALID"}""",
            HttpStatusCode.BadRequest));

        KronxyApiException error = await Assert.ThrowsAsync<KronxyApiException>(
            () => client.AdvanceJobAsync(
                Guid.Parse("10000000-0000-4000-8000-000000000001")));

        Assert.Equal("Job.PlanningExecutionFailed", error.Code);
        Assert.Equal("PLANNING_PATH_COHERENCE_INVALID", error.DiagnosticCode);
        Assert.StartsWith("web-advance-", error.CorrelationId);
    }

    [Fact]
    public void Artifact_identity_is_api_relative_not_filesystem_path()
    {
        var artifact = new ArtifactDto(Guid.NewGuid(), "BuildReport", "/api/jobs/a/artifacts/b",
            new string('a', 64), DateTimeOffset.UtcNow, "build", 12);
        Assert.StartsWith("/api/jobs/", artifact.ApiPath);
        Assert.DoesNotContain("/home/", artifact.ApiPath);
        Assert.DoesNotContain("..", artifact.ApiPath);
    }

    private const string JobJson = """{"id":"10000000-0000-4000-8000-000000000001","externalId":"KRX-1","state":"Created","runId":"10000000-0000-4000-8000-000000000001"}""";

    private static KronxyApiClient Client(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") }, new TestIdentity());

    private sealed class RecordingHandler(string body, HttpStatusCode status = HttpStatusCode.OK,
        string mediaType = "application/json") : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> Bodies { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests.Add(request);
            if (request.Content is not null) Bodies.Add(request.Content.ReadAsStringAsync(token).GetAwaiter().GetResult());
            return Task.FromResult(new HttpResponseMessage(status)
            { Content = new StringContent(body, Encoding.UTF8, mediaType) });
        }
    }

    private sealed class RouteHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests.Add(request);
            string path = request.RequestUri!.AbsolutePath;
            string body = path switch
            {
                "/health" => """{"status":"Healthy","api":"Healthy","database":"Healthy"}""",
                _ when path.EndsWith("/history") || path.EndsWith("/artifacts") || path.EndsWith("/allowed-actions") => "[]",
                _ when path.EndsWith("/effective-lineage") => """{"lineageType":"Developer","correlationId":"x","runId":"10000000-0000-4000-8000-000000000001","attemptCount":1,"sourceMutationPresent":true,"observedChangesAvailable":true}""",
                _ when path.Contains("/artifacts/") => "{}",
                _ => """{"id":"10000000-0000-4000-8000-000000000001","externalId":"KRX-1","state":"Created","runId":"10000000-0000-4000-8000-000000000001"}"""
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(body, Encoding.UTF8, path.Contains("/artifacts/") ? "application/octet-stream" : "application/json") });
        }
    }

    private sealed class TestIdentity : IOperatorIdentity
    {
        public string Actor => "LocalOperator";
        public string NewCorrelationId(string operation) => $"web-{operation}-{Guid.NewGuid():N}";
    }
}
