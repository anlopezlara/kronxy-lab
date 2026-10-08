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
    public void Artifact_identity_is_api_relative_not_filesystem_path()
    {
        var artifact = new ArtifactDto(Guid.NewGuid(), "BuildReport", "/api/jobs/a/artifacts/b",
            new string('a', 64), DateTimeOffset.UtcNow, "build", 12);
        Assert.StartsWith("/api/jobs/", artifact.ApiPath);
        Assert.DoesNotContain("/home/", artifact.ApiPath);
        Assert.DoesNotContain("..", artifact.ApiPath);
    }

    private static KronxyApiClient Client(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") });

    private sealed class RecordingHandler(string body, HttpStatusCode status = HttpStatusCode.OK,
        string mediaType = "application/json") : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests.Add(request);
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
}
