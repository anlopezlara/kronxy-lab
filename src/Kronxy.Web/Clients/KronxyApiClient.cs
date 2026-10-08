using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kronxy.Web.Models;

namespace Kronxy.Web.Clients;

public sealed class KronxyApiClient(HttpClient httpClient) : IKronxyApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<HealthDto> GetHealthAsync(CancellationToken cancellationToken = default) =>
        GetAsync<HealthDto>("health", cancellationToken);

    public Task<JobPageDto> GetJobsAsync(int page, int pageSize, string? externalId = null,
        string? state = null, CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(externalId)) query.Add($"externalId={Uri.EscapeDataString(externalId.Trim())}");
        if (!string.IsNullOrWhiteSpace(state)) query.Add($"state={Uri.EscapeDataString(state.Trim())}");
        return GetAsync<JobPageDto>($"api/jobs?{string.Join('&', query)}", cancellationToken);
    }

    public Task<JobDetailDto> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        GetAsync<JobDetailDto>($"api/jobs/{jobId:D}", cancellationToken);

    public Task<IReadOnlyList<JobHistoryDto>> GetJobHistoryAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<JobHistoryDto>>($"api/jobs/{jobId:D}/history", cancellationToken);

    public Task<IReadOnlyList<ArtifactDto>> GetJobArtifactsAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<ArtifactDto>>($"api/jobs/{jobId:D}/artifacts", cancellationToken);

    public async Task<ArtifactContentDto> GetArtifactAsync(Guid jobId, Guid artifactId,
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await SendAsync(
            new HttpRequestMessage(HttpMethod.Get, $"api/jobs/{jobId:D}/artifacts/{artifactId:D}"), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        string contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        string? fileName = response.Content.Headers.ContentDisposition?.FileNameStar ??
            response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        return new(await response.Content.ReadAsByteArrayAsync(cancellationToken), contentType, fileName);
    }

    public async Task<LineageDto?> GetEffectiveLineageAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        try { return await GetAsync<LineageDto>($"api/jobs/{jobId:D}/effective-lineage", cancellationToken); }
        catch (KronxyApiException exception) when (exception.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound) { return null; }
    }

    public Task<IReadOnlyList<AllowedActionDto>> GetAllowedActionsAsync(Guid jobId,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<AllowedActionDto>>($"api/jobs/{jobId:D}/allowed-actions", cancellationToken);

    private async Task<T> GetAsync<T>(string uri, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        T? result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return result ?? throw new KronxyApiException(response.StatusCode, "API_RESPONSE_EMPTY", "The API returned an empty response.");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try { return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken); }
        catch (HttpRequestException exception) { throw new KronxyApiException(null, "API_UNREACHABLE", "KRONXY API is unavailable.", exception); }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested) { throw new KronxyApiException(null, "API_TIMEOUT", "KRONXY API did not respond in time.", exception); }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        ApiErrorDto? error = null;
        try { error = await response.Content.ReadFromJsonAsync<ApiErrorDto>(JsonOptions, cancellationToken); } catch (JsonException) { }
        string code = string.IsNullOrWhiteSpace(error?.Code) ? $"HTTP_{(int)response.StatusCode}" : error.Code;
        string message = string.IsNullOrWhiteSpace(error?.Message) ? KronxyApiException.Category(response.StatusCode) : error.Message;
        throw new KronxyApiException(response.StatusCode, code, message);
    }
}
