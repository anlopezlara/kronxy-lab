using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kronxy.Web.Models;

namespace Kronxy.Web.Clients;

public sealed class KronxyApiClient(HttpClient httpClient, IOperatorIdentity identity) : IKronxyApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<HealthDto> GetHealthAsync(CancellationToken cancellationToken = default) => GetAsync<HealthDto>("health", cancellationToken);

    public Task<JobPageDto> GetJobsAsync(int page, int pageSize, string? externalId = null, string? state = null, CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(externalId)) query.Add($"externalId={Uri.EscapeDataString(externalId.Trim())}");
        if (!string.IsNullOrWhiteSpace(state)) query.Add($"state={Uri.EscapeDataString(state.Trim())}");
        return GetAsync<JobPageDto>($"api/jobs?{string.Join('&', query)}", cancellationToken);
    }

    public Task<JobDetailDto> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default) => GetAsync<JobDetailDto>($"api/jobs/{jobId:D}", cancellationToken);
    public Task<IReadOnlyList<JobHistoryDto>> GetJobHistoryAsync(Guid jobId, CancellationToken cancellationToken = default) => GetAsync<IReadOnlyList<JobHistoryDto>>($"api/jobs/{jobId:D}/history", cancellationToken);
    public Task<IReadOnlyList<ArtifactDto>> GetJobArtifactsAsync(Guid jobId, CancellationToken cancellationToken = default) => GetAsync<IReadOnlyList<ArtifactDto>>($"api/jobs/{jobId:D}/artifacts", cancellationToken);

    public async Task<ArtifactContentDto> GetArtifactAsync(Guid jobId, Guid artifactId, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await SendAsync(new(HttpMethod.Get, $"api/jobs/{jobId:D}/artifacts/{artifactId:D}"), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        string contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        string? fileName = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        return new(await response.Content.ReadAsByteArrayAsync(cancellationToken), contentType, fileName);
    }

    public async Task<LineageDto?> GetEffectiveLineageAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        try { return await GetAsync<LineageDto>($"api/jobs/{jobId:D}/effective-lineage", cancellationToken); }
        catch (KronxyApiException exception) when (exception.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound) { return null; }
    }

    public Task<IReadOnlyList<AllowedActionDto>> GetAllowedActionsAsync(Guid jobId, CancellationToken cancellationToken = default) => GetAsync<IReadOnlyList<AllowedActionDto>>($"api/jobs/{jobId:D}/allowed-actions", cancellationToken);
    public Task<JobDetailDto> CreateJobAsync(string request, string? externalId, CancellationToken cancellationToken = default) => PostAsync<JobDetailDto>("api/jobs", new { request, externalId }, cancellationToken);
    public Task<OperationResultDto> AdvanceJobAsync(Guid jobId, CancellationToken cancellationToken = default) => ActionAsync(jobId, "advance", "advance", cancellationToken);
    public Task<OperationResultDto> ResumeJobAsync(Guid jobId, string reason, CancellationToken cancellationToken = default) => ReasonActionAsync(jobId, "resume", "resume", reason, cancellationToken);
    public Task<OperationResultDto> RetryPendingAsync(Guid jobId, string reason, CancellationToken cancellationToken = default) => ReasonActionAsync(jobId, "retry-pending", "retry-pending", reason, cancellationToken);
    public Task<OperationResultDto> CancelJobAsync(Guid jobId, CancellationToken cancellationToken = default) => ActionAsync(jobId, "cancel", "cancel", cancellationToken);
    public Task<OperationResultDto> ApproveHumanReviewAsync(Guid jobId, CancellationToken cancellationToken = default) => ActionAsync(jobId, "human-review/approve", "human-review-approve", cancellationToken);

    public Task<OperationResultDto> RequestHumanReviewChangesAsync(Guid jobId, IReadOnlyList<HumanReviewCorrectionDto> corrections, CancellationToken cancellationToken = default) =>
        PostGovernedAsync<OperationResultDto>($"api/jobs/{jobId:D}/human-review/changes-required", "human-review-changes-required", correlationId => new { requiredCorrections = corrections, actor = identity.Actor, correlationId }, cancellationToken);

    public Task<OperationResultDto> ResolveArchitectureDecisionAsync(Guid jobId, ArchitectureDecisionInputDto decision, CancellationToken cancellationToken = default) =>
        PostGovernedAsync<OperationResultDto>($"api/jobs/{jobId:D}/architecture-decision", "architecture-decision", correlationId => new { actor = identity.Actor, correlationId, decision = (int)decision.Decision, decision.Reason, decision.FollowUpRequired, decision.FollowUpDescription }, cancellationToken);

    public Task<OperationResultDto> ApplyHumanCorrectionAsync(Guid jobId, string reason, IReadOnlyList<HumanFileReplacementDto> changes, CancellationToken cancellationToken = default) =>
        PostGovernedAsync<OperationResultDto>($"api/jobs/{jobId:D}/human-correction", "human-correction", correlationId => new { changes, actor = identity.Actor, correlationId, reason }, cancellationToken);

    public Task<OperationResultDto> SupersedeReviewerCorrectionAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        ActionAsync(jobId, "reviewer/human-review-correction/supersede", "reviewer-supersede", cancellationToken);

    private Task<OperationResultDto> ActionAsync(Guid jobId, string route, string operation, CancellationToken cancellationToken)
    {
        string correlationId = identity.NewCorrelationId(operation);
        return PostActionAsync<OperationResultDto>(
            $"api/jobs/{jobId:D}/{route}",
            new { actor = identity.Actor, correlationId },
            correlationId,
            cancellationToken);
    }

    private Task<OperationResultDto> ReasonActionAsync(Guid jobId, string route, string operation, string reason, CancellationToken cancellationToken)
    {
        string correlationId = identity.NewCorrelationId(operation);
        return PostActionAsync<OperationResultDto>(
            $"api/jobs/{jobId:D}/{route}",
            new { reason, actor = identity.Actor, correlationId },
            correlationId,
            cancellationToken);
    }

    private async Task<T> GetAsync<T>(string uri, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await SendAsync(new(HttpMethod.Get, uri), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadRequiredJsonAsync<T>(response, cancellationToken);
    }

    private async Task<T> PostAsync<T>(string uri, object body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(body, options: JsonOptions) };
        HttpResponseMessage response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadRequiredJsonAsync<T>(response, cancellationToken);
    }

    private async Task<T> PostActionAsync<T>(string uri, object body,
        string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            return await PostAsync<T>(uri, body, cancellationToken);
        }
        catch (KronxyApiException exception)
        {
            throw exception.WithCorrelationId(correlationId);
        }
    }

    private Task<T> PostGovernedAsync<T>(string uri, string operation,
        Func<string, object> body, CancellationToken cancellationToken)
    {
        string correlationId = identity.NewCorrelationId(operation);
        return PostActionAsync<T>(
            uri,
            body(correlationId),
            correlationId,
            cancellationToken);
    }

    private static async Task<T> ReadRequiredJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken) ?? throw new KronxyApiException(response.StatusCode, "API_RESPONSE_EMPTY", "The API returned an empty response.");

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try { return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken); }
        catch (HttpRequestException exception) { throw new KronxyApiException(null, "API_UNREACHABLE", "KRONXY API is unavailable.", exception); }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested) { throw new KronxyApiException(null, "API_TIMEOUT", "KRONXY API did not respond in time.", exception); }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        string code = $"HTTP_{(int)response.StatusCode}";
        string message = KronxyApiException.Category(response.StatusCode);
        string? diagnosticCode = null;
        try
        {
            using JsonDocument document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            JsonElement root = document.RootElement;
            code = Text(root, "code") ?? Text(root, "type") ?? code;
            message = Text(root, "message") ?? Text(root, "detail") ?? Text(root, "title") ?? message;
            diagnosticCode = Text(root, "diagnosticCode");
        }
        catch (JsonException) { }
        throw new KronxyApiException(
            response.StatusCode,
            code,
            message,
            diagnosticCode: diagnosticCode);
    }

    private static string? Text(JsonElement element, string property) => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
