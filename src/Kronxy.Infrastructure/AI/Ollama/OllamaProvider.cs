using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kronxy.Application.AI;

namespace Kronxy.Infrastructure.AI.Ollama;

public sealed class OllamaProvider : IAiProvider
{
    private const string ProviderName = "Ollama";

    private readonly HttpClient httpClient;
    private readonly AiGatewayOptions options;

    public OllamaProvider(
        HttpClient httpClient,
        AiGatewayOptions options)
    {
        this.httpClient =
            httpClient ??
            throw new ArgumentNullException(
                nameof(httpClient));

        this.options =
            options ??
            throw new ArgumentNullException(
                nameof(options));

        options.Validate();
    }

    public async Task<AiResponse> GenerateAsync(
        AiRequest request,
        string physicalModel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(
                physicalModel))
        {
            return Failure(
                AiOperationStatus.Rejected,
                physicalModel,
                "OLLAMA_MODEL_REQUIRED");
        }

        var payload =
            new OllamaChatRequest
            {
                Model =
                    physicalModel,

                Stream =
                    false,

                Think =
                    false,

                Messages =
                [
                    new OllamaMessage
                    {
                        Role = "system",
                        Content =
                            request.SystemInstructions
                    },
                    new OllamaMessage
                    {
                        Role = "user",
                        Content =
                            request.UserContent
                    }
                ],

                Options =
                    new OllamaRequestOptions
                    {
                        NumContext =
                            request.Generation.ContextWindowTokens ??
                            options.ContextWindowTokens,

                        NumPredict =
                            request.Generation
                                .MaxOutputTokens,

                        Temperature =
                            request.Generation
                                .Temperature
                    },

                Format =
                    request.StructuredOutput?
                        .Schema
            };

        Stopwatch stopwatch =
            Stopwatch.StartNew();

        HttpResponseMessage response;

        try
        {
            using var httpRequest =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    "api/chat")
                {
                    Content =
                        JsonContent.Create(
                            payload)
                };

            response =
                await httpClient
                    .SendAsync(
                        httpRequest,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            return Failure(
                AiOperationStatus.ProviderUnavailable,
                physicalModel,
                "OLLAMA_CONNECTION_TIMEOUT",
                stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            stopwatch.Stop();

            return Failure(
                AiOperationStatus.ProviderUnavailable,
                physicalModel,
                "OLLAMA_UNAVAILABLE",
                stopwatch.Elapsed);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                stopwatch.Stop();

                return Failure(
                    AiOperationStatus.ProviderError,
                    physicalModel,
                    MapHttpError(
                        response.StatusCode),
                    stopwatch.Elapsed);
            }

            if (IsResponseDeclaredTooLarge(
                    response.Content))
            {
                stopwatch.Stop();

                return Failure(
                    AiOperationStatus.InvalidResponse,
                    physicalModel,
                    "OLLAMA_RESPONSE_TOO_LARGE",
                    stopwatch.Elapsed);
            }

            OllamaChatResponse? body;

            try
            {
                body =
                    await ReadBoundedJsonAsync<
                        OllamaChatResponse>(
                        response.Content,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (AiResponseTooLargeException)
            {
                stopwatch.Stop();

                return Failure(
                    AiOperationStatus.InvalidResponse,
                    physicalModel,
                    "OLLAMA_RESPONSE_TOO_LARGE",
                    stopwatch.Elapsed);
            }
            catch (JsonException)
            {
                stopwatch.Stop();

                return Failure(
                    AiOperationStatus.InvalidResponse,
                    physicalModel,
                    "OLLAMA_INVALID_JSON",
                    stopwatch.Elapsed);
            }

            stopwatch.Stop();

            if (body is null ||
                body.Message is null ||
                body.Message.Content is null)
            {
                return Failure(
                    AiOperationStatus.InvalidResponse,
                    physicalModel,
                    "OLLAMA_INVALID_RESPONSE",
                    stopwatch.Elapsed);
            }

            if (!body.Done)
            {
                return new AiResponse
                {
                    Status = AiOperationStatus.InvalidResponse,
                    Content = body.Message.Content,
                    Provider = ProviderName,
                    PhysicalModel = body.Model ?? physicalModel,
                    Duration = stopwatch.Elapsed,
                    TerminationReason =
                        MapTerminationReason(body.DoneReason),
                    Usage = new AiUsage(
                        body.PromptEvalCount,
                        body.EvalCount),
                    ProviderMetadata = Metadata(body),
                    ErrorCode = "OLLAMA_INCOMPLETE_RESPONSE"
                };
            }

            if (string.IsNullOrWhiteSpace(
                    body.Message.Content))
            {
                return Failure(
                    AiOperationStatus.InvalidResponse,
                    physicalModel,
                    "OLLAMA_EMPTY_CONTENT",
                    stopwatch.Elapsed);
            }

            return new AiResponse
            {
                Status =
                    AiOperationStatus.Success,

                Content =
                    body.Message.Content,

                Provider =
                    ProviderName,

                PhysicalModel =
                    body.Model ??
                    physicalModel,

                Duration =
                    stopwatch.Elapsed,

                TerminationReason =
                    MapTerminationReason(
                        body.DoneReason),

                Usage =
                    new AiUsage(
                        body.PromptEvalCount,
                        body.EvalCount),

                ProviderMetadata = Metadata(body)
            };
        }
    }

    public async Task<AiProviderHealthResult>
        CheckHealthAsync(
            IReadOnlyCollection<string>
                requiredPhysicalModels,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            requiredPhysicalModels);

        HttpResponseMessage response;

        try
        {
            using var httpRequest =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    "api/tags");

            response =
                await httpClient
                    .SendAsync(
                        httpRequest,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return new AiProviderHealthResult(
                AiProviderHealthStatus.Unavailable,
                ProviderName,
                Array.Empty<string>(),
                "OLLAMA_CONNECTION_TIMEOUT");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return new AiProviderHealthResult(
                AiProviderHealthStatus.Unavailable,
                ProviderName,
                Array.Empty<string>(),
                "OLLAMA_UNAVAILABLE");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return new AiProviderHealthResult(
                    AiProviderHealthStatus.Unavailable,
                    ProviderName,
                    Array.Empty<string>(),
                    MapHttpError(
                        response.StatusCode));
            }

            if (IsResponseDeclaredTooLarge(
                    response.Content))
            {
                return new AiProviderHealthResult(
                    AiProviderHealthStatus.Unavailable,
                    ProviderName,
                    Array.Empty<string>(),
                    "OLLAMA_RESPONSE_TOO_LARGE");
            }

            OllamaTagsResponse? body;

            try
            {
                body =
                    await ReadBoundedJsonAsync<
                        OllamaTagsResponse>(
                        response.Content,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (AiResponseTooLargeException)
            {
                return new AiProviderHealthResult(
                    AiProviderHealthStatus.Unavailable,
                    ProviderName,
                    Array.Empty<string>(),
                    "OLLAMA_RESPONSE_TOO_LARGE");
            }
            catch (JsonException)
            {
                return new AiProviderHealthResult(
                    AiProviderHealthStatus.Unavailable,
                    ProviderName,
                    Array.Empty<string>(),
                    "OLLAMA_INVALID_JSON");
            }

            if (body?.Models is null)
            {
                return new AiProviderHealthResult(
                    AiProviderHealthStatus.Unavailable,
                    ProviderName,
                    Array.Empty<string>(),
                    "OLLAMA_INVALID_RESPONSE");
            }

            var available =
                new HashSet<string>(
                    body.Models
                        .Where(
                            model =>
                                !string.IsNullOrWhiteSpace(
                                    model.Name))
                        .Select(
                            model =>
                                model.Name!),
                    StringComparer.Ordinal);

            string[] missing =
                requiredPhysicalModels
                    .Where(
                        model =>
                            !available.Contains(
                                model))
                    .Distinct(
                        StringComparer.Ordinal)
                    .ToArray();

            if (missing.Length > 0)
            {
                return new AiProviderHealthResult(
                    AiProviderHealthStatus.Misconfigured,
                    ProviderName,
                    missing,
                    "OLLAMA_REQUIRED_MODEL_MISSING");
            }

            return new AiProviderHealthResult(
                AiProviderHealthStatus.Available,
                ProviderName,
                Array.Empty<string>(),
                string.Empty);
        }
    }

    private static AiResponse Failure(
        AiOperationStatus status,
        string physicalModel,
        string errorCode,
        TimeSpan duration = default)
    {
        return new AiResponse
        {
            Status =
                status,

            Provider =
                ProviderName,

            PhysicalModel =
                physicalModel,

            Duration =
                duration,

            TerminationReason =
                AiTerminationReason.Error,

            ErrorCode =
                errorCode
        };
    }

    private bool IsResponseDeclaredTooLarge(
        HttpContent content)
    {
        long? contentLength =
            content.Headers.ContentLength;

        return contentLength.HasValue &&
            contentLength.Value >
                options.MaxResponseBytes;
    }

    private async Task<T?> ReadBoundedJsonAsync<T>(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        await using Stream stream =
            await content
                .ReadAsStreamAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        using var buffer =
            new MemoryStream();

        byte[] chunk =
            new byte[8192];

        int total =
            0;

        while (true)
        {
            int remaining =
                options.MaxResponseBytes -
                total;

            if (remaining <= 0)
            {
                int extra =
                    await stream.ReadAsync(
                        chunk.AsMemory(
                            0,
                            1),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (extra > 0)
                {
                    throw new AiResponseTooLargeException();
                }

                break;
            }

            int readSize =
                Math.Min(
                    chunk.Length,
                    remaining);

            int read =
                await stream.ReadAsync(
                    chunk.AsMemory(
                        0,
                        readSize),
                    cancellationToken)
                .ConfigureAwait(false);

            if (read == 0)
            {
                break;
            }

            await buffer.WriteAsync(
                chunk.AsMemory(
                    0,
                    read),
                cancellationToken)
                .ConfigureAwait(false);

            total +=
                read;
        }

        buffer.Position =
            0;

        return await JsonSerializer
            .DeserializeAsync<T>(
                buffer,
                new JsonSerializerOptions(
                    JsonSerializerDefaults.Web),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed class AiResponseTooLargeException :
        Exception
    {
    }

    private static string MapHttpError(
        HttpStatusCode statusCode)
    {
        return $"OLLAMA_HTTP_{(int)statusCode}";
    }

    private static AiTerminationReason
        MapTerminationReason(
            string? doneReason)
    {
        return doneReason switch
        {
            "stop" =>
                AiTerminationReason.Stop,

            "length" =>
                AiTerminationReason.Length,

            _ =>
                AiTerminationReason.Unknown
        };
    }

    private static AiProviderResponseMetadata Metadata(
        OllamaChatResponse body) =>
        new()
        {
            Done = body.Done,
            DoneReason = body.DoneReason,
            PromptEvalCount = body.PromptEvalCount,
            EvalCount = body.EvalCount,
            TotalDurationNanoseconds = body.TotalDuration,
            PromptEvalDurationNanoseconds = body.PromptEvalDuration,
            EvalDurationNanoseconds = body.EvalDuration
        };

    private sealed record OllamaChatRequest
    {
        public required string Model
        {
            get;
            init;
        }

        public bool Stream
        {
            get;
            init;
        }

        [JsonPropertyName("think")]
        public bool Think
        {
            get;
            init;
        }

        public required IReadOnlyList<OllamaMessage>
            Messages
        {
            get;
            init;
        }

        public OllamaRequestOptions? Options
        {
            get;
            init;
        }

        public JsonElement? Format
        {
            get;
            init;
        }
    }

    private sealed record OllamaRequestOptions
    {
        [JsonPropertyName("num_ctx")]
        public int NumContext
        {
            get;
            init;
        }

        [JsonPropertyName("num_predict")]
        public int? NumPredict
        {
            get;
            init;
        }

        public double? Temperature
        {
            get;
            init;
        }
    }

    private sealed record OllamaMessage
    {
        public required string Role
        {
            get;
            init;
        }

        public required string Content
        {
            get;
            init;
        }
    }

    private sealed record OllamaChatResponse
    {
        public string? Model
        {
            get;
            init;
        }

        public OllamaMessage? Message
        {
            get;
            init;
        }

        public bool Done
        {
            get;
            init;
        }

        [JsonPropertyName("done_reason")]
        public string? DoneReason
        {
            get;
            init;
        }

        [JsonPropertyName("prompt_eval_count")]
        public long? PromptEvalCount
        {
            get;
            init;
        }

        [JsonPropertyName("eval_count")]
        public long? EvalCount
        {
            get;
            init;
        }

        [JsonPropertyName("total_duration")]
        public long? TotalDuration { get; init; }

        [JsonPropertyName("prompt_eval_duration")]
        public long? PromptEvalDuration { get; init; }

        [JsonPropertyName("eval_duration")]
        public long? EvalDuration { get; init; }
    }

    private sealed record OllamaTagsResponse
    {
        public IReadOnlyList<OllamaModelInfo>? Models
        {
            get;
            init;
        }
    }

    private sealed record OllamaModelInfo
    {
        public string? Name
        {
            get;
            init;
        }
    }
}
