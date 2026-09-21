using System.Net;
using System.Text;
using System.Text.Json;
using Kronxy.Application.AI;
using Kronxy.Infrastructure.AI;
using Kronxy.Infrastructure.AI.Ollama;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class OllamaProviderTests
{
    [Fact]
    public async Task GenerateAsync_returns_success_and_usage()
    {
        var handler =
            new StubHttpMessageHandler(
                request =>
                {
                    Assert.Equal(
                        HttpMethod.Post,
                        request.Method);

                    Assert.Equal(
                        "/api/chat",
                        request.RequestUri!.AbsolutePath);

                    return JsonResponse(
                        """
                        {
                          "model": "model-fast",
                          "message": {
                            "role": "assistant",
                            "content": "KRONXY_OK"
                          },
                          "done": true,
                          "done_reason": "stop",
                          "prompt_eval_count": 11,
                          "eval_count": 4
                        }
                        """);
                });

        using var client =
            CreateClient(handler);

        var provider =
            CreateProvider(
                client);

        AiResponse result =
            await provider.GenerateAsync(
                CreateRequest(),
                "model-fast");

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "KRONXY_OK",
            result.Content);
        Assert.Equal(
            "model-fast",
            result.PhysicalModel);
        Assert.Equal(
            "Ollama",
            result.Provider);
        Assert.Equal(
            AiTerminationReason.Stop,
            result.TerminationReason);
        Assert.Equal(
            11,
            result.Usage.PromptTokens);
        Assert.Equal(
            4,
            result.Usage.CompletionTokens);
    }

    [Fact]
    public async Task GenerateAsync_sends_roles_limits_and_non_streaming_payload()
    {
        string? captured =
            null;

        var handler =
            new StubHttpMessageHandler(
                async request =>
                {
                    captured =
                        await request.Content!
                            .ReadAsStringAsync();

                    return JsonResponse(
                        """
                        {
                          "model": "model-fast",
                          "message": {
                            "role": "assistant",
                            "content": "ok"
                          },
                          "done": true,
                          "done_reason": "stop"
                        }
                        """);
                });

        using var client =
            CreateClient(handler);

        var provider =
            CreateProvider(
                client);

        await provider.GenerateAsync(
            CreateRequest(),
            "model-fast");

        Assert.NotNull(captured);

        using JsonDocument document =
            JsonDocument.Parse(captured!);

        JsonElement root =
            document.RootElement;

        Assert.Equal(
            "model-fast",
            root.GetProperty("model").GetString());

        Assert.False(
            root.GetProperty("stream").GetBoolean());

        Assert.False(
            root.GetProperty("think").GetBoolean());

        JsonElement messages =
            root.GetProperty("messages");

        Assert.Equal(
            2,
            messages.GetArrayLength());

        Assert.Equal(
            "system",
            messages[0]
                .GetProperty("role")
                .GetString());

        Assert.Equal(
            "system instructions",
            messages[0]
                .GetProperty("content")
                .GetString());

        Assert.Equal(
            "user",
            messages[1]
                .GetProperty("role")
                .GetString());

        Assert.Equal(
            "user content",
            messages[1]
                .GetProperty("content")
                .GetString());

        JsonElement options =
            root.GetProperty("options");

        Assert.Equal(
            16_384,
            options
                .GetProperty("num_ctx")
                .GetInt32());

        Assert.Equal(
            32,
            options
                .GetProperty("num_predict")
                .GetInt32());

        Assert.False(
            options.TryGetProperty(
                "numPredict",
                out _));

        Assert.Equal(
            0,
            options
                .GetProperty("temperature")
                .GetDouble());
    }

    [Fact]
    public async Task GenerateAsync_sends_structured_output_schema()
    {
        string? captured =
            null;

        var handler =
            new StubHttpMessageHandler(
                async request =>
                {
                    captured =
                        await request.Content!
                            .ReadAsStringAsync();

                    return JsonResponse(
                        """
                        {
                          "model": "model-fast",
                          "message": {
                            "role": "assistant",
                            "content": "{\"status\":\"ok\"}"
                          },
                          "done": true,
                          "done_reason": "stop"
                        }
                        """);
                });

        using var client =
            CreateClient(handler);

        var provider =
            CreateProvider(
                client);

        AiRequest request =
            CreateRequest() with
            {
                StructuredOutput =
                    new AiStructuredOutput
                    {
                        Schema =
                            JsonSerializer
                                .Deserialize<JsonElement>(
                                    """
                                    {
                                      "type": "object",
                                      "properties": {
                                        "status": {
                                          "type": "string"
                                        }
                                      },
                                      "required": [
                                        "status"
                                      ]
                                    }
                                    """)
                    }
            };

        await provider.GenerateAsync(
            request,
            "model-fast");

        Assert.NotNull(captured);

        using JsonDocument document =
            JsonDocument.Parse(captured!);

        JsonElement format =
            document.RootElement
                .GetProperty("format");

        Assert.Equal(
            "object",
            format
                .GetProperty("type")
                .GetString());

        Assert.True(
            format
                .GetProperty("properties")
                .TryGetProperty(
                    "status",
                    out _));
    }

    [Fact]
    public async Task GenerateAsync_maps_http_error()
    {
        var handler =
            new StubHttpMessageHandler(
                _ =>
                    new HttpResponseMessage(
                        HttpStatusCode
                            .InternalServerError));

        using var client =
            CreateClient(handler);

        var provider =
            CreateProvider(
                client);

        AiResponse result =
            await provider.GenerateAsync(
                CreateRequest(),
                "model-fast");

        Assert.Equal(
            AiOperationStatus.ProviderError,
            result.Status);

        Assert.Equal(
            "OLLAMA_HTTP_500",
            result.ErrorCode);
    }

    [Fact]
    public async Task GenerateAsync_rejects_invalid_json()
    {
        var handler =
            new StubHttpMessageHandler(
                _ =>
                    new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content =
                            new StringContent(
                                "{broken",
                                Encoding.UTF8,
                                "application/json")
                    });

        using var client =
            CreateClient(handler);

        var provider =
            CreateProvider(
                client);

        AiResponse result =
            await provider.GenerateAsync(
                CreateRequest(),
                "model-fast");

        Assert.Equal(
            AiOperationStatus.InvalidResponse,
            result.Status);

        Assert.Equal(
            "OLLAMA_INVALID_JSON",
            result.ErrorCode);
    }

    [Fact]
    public async Task GenerateAsync_rejects_missing_message()
    {
        var handler =
            new StubHttpMessageHandler(
                _ =>
                    JsonResponse(
                        """
                        {
                          "model": "model-fast",
                          "done": true,
                          "done_reason": "stop"
                        }
                        """));

        using var client =
            CreateClient(handler);

        var provider =
            CreateProvider(
                client);

        AiResponse result =
            await provider.GenerateAsync(
                CreateRequest(),
                "model-fast");

        Assert.Equal(
            AiOperationStatus.InvalidResponse,
            result.Status);

        Assert.Equal(
            "OLLAMA_INVALID_RESPONSE",
            result.ErrorCode);
    }

    [Fact]
    public async Task GenerateAsync_maps_transport_failure_to_unavailable()
    {
        var handler =
            new ThrowingHttpMessageHandler(
                new HttpRequestException(
                    "simulated"));

        using var client =
            CreateClient(handler);

        var provider =
            CreateProvider(
                client);

        AiResponse result =
            await provider.GenerateAsync(
                CreateRequest(),
                "model-fast");

        Assert.Equal(
            AiOperationStatus.ProviderUnavailable,
            result.Status);

        Assert.Equal(
            "OLLAMA_UNAVAILABLE",
            result.ErrorCode);
    }

    [Fact]
    public async Task CheckHealthAsync_returns_available_when_all_models_exist()
    {
        var handler =
            new StubHttpMessageHandler(
                request =>
                {
                    Assert.Equal(
                        HttpMethod.Get,
                        request.Method);

                    Assert.Equal(
                        "/api/tags",
                        request.RequestUri!.AbsolutePath);

                    return JsonResponse(
                        """
                        {
                          "models": [
                            {
                              "name": "model-fast"
                            },
                            {
                              "name": "model-quality"
                            },
                            {
                              "name": "model-general"
                            }
                          ]
                        }
                        """);
                });

        using var client =
            CreateClient(handler);

        var provider =
            CreateProvider(
                client);

        AiProviderHealthResult result =
            await provider.CheckHealthAsync(
                [
                    "model-fast",
                    "model-quality",
                    "model-general"
                ]);

        Assert.True(result.IsAvailable);
        Assert.Empty(result.MissingModels);
        Assert.Equal(
            string.Empty,
            result.ErrorCode);
    }

    [Fact]
    public async Task CheckHealthAsync_returns_misconfigured_when_model_is_missing()
    {
        var handler =
            new StubHttpMessageHandler(
                _ =>
                    JsonResponse(
                        """
                        {
                          "models": [
                            {
                              "name": "model-fast"
                            },
                            {
                              "name": "model-general"
                            }
                          ]
                        }
                        """));

        using var client =
            CreateClient(handler);

        var provider =
            CreateProvider(
                client);

        AiProviderHealthResult result =
            await provider.CheckHealthAsync(
                [
                    "model-fast",
                    "model-quality",
                    "model-general"
                ]);

        Assert.Equal(
            AiProviderHealthStatus.Misconfigured,
            result.Status);

        Assert.Single(
            result.MissingModels);

        Assert.Equal(
            "model-quality",
            result.MissingModels[0]);

        Assert.Equal(
            "OLLAMA_REQUIRED_MODEL_MISSING",
            result.ErrorCode);
    }

    [Fact]
    public async Task CheckHealthAsync_rejects_invalid_json()
    {
        var handler =
            new StubHttpMessageHandler(
                _ =>
                    new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content =
                            new StringContent(
                                "{bad",
                                Encoding.UTF8,
                                "application/json")
                    });

        using var client =
            CreateClient(handler);

        var provider =
            CreateProvider(
                client);

        AiProviderHealthResult result =
            await provider.CheckHealthAsync(
                ["model-fast"]);

        Assert.Equal(
            AiProviderHealthStatus.Unavailable,
            result.Status);

        Assert.Equal(
            "OLLAMA_INVALID_JSON",
            result.ErrorCode);
    }

    [Fact]
    public async Task GenerateAsync_rejects_response_above_byte_limit()
    {
        string json =
            """
            {
              "model": "model-fast",
              "message": {
                "role": "assistant",
                "content": "THIS_CONTENT_IS_TOO_LARGE"
              },
              "done": true,
              "done_reason": "stop"
            }
            """;

        var handler =
            new StubHttpMessageHandler(
                _ =>
                    JsonResponse(
                        json));

        using var client =
            CreateClient(handler);

        var provider =
            CreateProvider(
                client,
                maxResponseBytes: 32);

        AiResponse result =
            await provider.GenerateAsync(
                CreateRequest(),
                "model-fast");

        Assert.Equal(
            AiOperationStatus.InvalidResponse,
            result.Status);

        Assert.Equal(
            "OLLAMA_RESPONSE_TOO_LARGE",
            result.ErrorCode);
    }

    [Fact]
    public async Task CheckHealthAsync_rejects_response_above_byte_limit()
    {
        string json =
            """
            {
              "models": [
                {
                  "name": "model-fast"
                },
                {
                  "name": "model-quality"
                }
              ]
            }
            """;

        var handler =
            new StubHttpMessageHandler(
                _ =>
                    JsonResponse(
                        json));

        using var client =
            CreateClient(handler);

        var provider =
            CreateProvider(
                client,
                maxResponseBytes: 24);

        AiProviderHealthResult result =
            await provider.CheckHealthAsync(
                ["model-fast"]);

        Assert.Equal(
            AiProviderHealthStatus.Unavailable,
            result.Status);

        Assert.Equal(
            "OLLAMA_RESPONSE_TOO_LARGE",
            result.ErrorCode);
    }

    [Fact]
    public async Task GenerateAsync_maps_internal_transport_timeout_to_unavailable()
    {
        var handler =
            new StubHttpMessageHandler(
                (Func<HttpRequestMessage, HttpResponseMessage>)(
                    _ =>
                        throw new OperationCanceledException()));

        using var client =
            CreateClient(handler);

        var provider =
            CreateProvider(
                client);

        AiResponse result =
            await provider.GenerateAsync(
                CreateRequest(),
                "model-fast");

        Assert.Equal(
            AiOperationStatus.ProviderUnavailable,
            result.Status);

        Assert.Equal(
            "OLLAMA_CONNECTION_TIMEOUT",
            result.ErrorCode);
    }

    [Fact]
    public async Task GenerateAsync_propagates_caller_cancellation()
    {
        var handler =
            new StubHttpMessageHandler(
                (Func<HttpRequestMessage, HttpResponseMessage>)(
                    _ =>
                        throw new OperationCanceledException()));

        using var client =
            CreateClient(handler);

        var provider =
            CreateProvider(
                client);

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
            () =>
                provider.GenerateAsync(
                    CreateRequest(),
                    "model-fast",
                    cancellation.Token));
    }

    [Fact]
    public async Task CheckHealthAsync_maps_internal_transport_timeout_to_unavailable()
    {
        var handler =
            new StubHttpMessageHandler(
                (Func<HttpRequestMessage, HttpResponseMessage>)(
                    _ =>
                        throw new OperationCanceledException()));

        using var client =
            CreateClient(handler);

        var provider =
            CreateProvider(
                client);

        AiProviderHealthResult result =
            await provider.CheckHealthAsync(
                ["model-fast"]);

        Assert.Equal(
            AiProviderHealthStatus.Unavailable,
            result.Status);

        Assert.Equal(
            "OLLAMA_CONNECTION_TIMEOUT",
            result.ErrorCode);
    }

    [Fact]
    public async Task GenerateAsync_rejects_done_false()
    {
        var handler =
            new StubHttpMessageHandler(
                _ =>
                    JsonResponse(
                        """
                        {
                          "model": "model-fast",
                          "message": {
                            "role": "assistant",
                            "content": "partial"
                          },
                          "done": false,
                          "done_reason": "length",
                          "prompt_eval_count": 321,
                          "eval_count": 2048,
                          "total_duration": 90000000000,
                          "prompt_eval_duration": 12000000000,
                          "eval_duration": 78000000000
                        }
                        """));

        using var client =
            CreateClient(handler);

        var provider =
            CreateProvider(
                client);

        AiResponse result =
            await provider.GenerateAsync(
                CreateRequest(),
                "model-fast");

        Assert.Equal(
            AiOperationStatus.InvalidResponse,
            result.Status);

        Assert.Equal(
            "OLLAMA_INCOMPLETE_RESPONSE",
            result.ErrorCode);
        Assert.Equal("partial", result.Content);
        Assert.Equal(321, result.Usage.PromptTokens);
        Assert.Equal(2048, result.Usage.CompletionTokens);
        Assert.False(result.ProviderMetadata!.Done);
        Assert.Equal("length", result.ProviderMetadata.DoneReason);
        Assert.Equal(
            90_000_000_000,
            result.ProviderMetadata.TotalDurationNanoseconds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GenerateAsync_rejects_empty_or_whitespace_content(
        string content)
    {
        string json =
            JsonSerializer.Serialize(
                new
                {
                    model =
                        "model-fast",

                    message =
                        new
                        {
                            role =
                                "assistant",

                            content
                        },

                    done =
                        true,

                    done_reason =
                        "stop"
                });

        var handler =
            new StubHttpMessageHandler(
                _ =>
                    JsonResponse(
                        json));

        using var client =
            CreateClient(handler);

        var provider =
            CreateProvider(
                client);

        AiResponse result =
            await provider.GenerateAsync(
                CreateRequest(),
                "model-fast");

        Assert.Equal(
            AiOperationStatus.InvalidResponse,
            result.Status);

        Assert.Equal(
            "OLLAMA_EMPTY_CONTENT",
            result.ErrorCode);
    }

    [Fact]
    public async Task GenerateAsync_rejects_declared_oversize_before_reading_body()
    {
        var content =
            new ThrowIfReadContent(
                declaredLength:
                    1048577);

        var handler =
            new StubHttpMessageHandler(
                _ =>
                    new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content =
                            content
                    });

        using var client =
            CreateClient(handler);

        var provider =
            CreateProvider(
                client,
                maxResponseBytes:
                    1048576);

        AiResponse result =
            await provider.GenerateAsync(
                CreateRequest(),
                "model-fast");

        Assert.Equal(
            AiOperationStatus.InvalidResponse,
            result.Status);

        Assert.Equal(
            "OLLAMA_RESPONSE_TOO_LARGE",
            result.ErrorCode);

        Assert.False(
            content.WasRead);
    }

    [Fact]
    public async Task CheckHealthAsync_rejects_declared_oversize_before_reading_body()
    {
        var content =
            new ThrowIfReadContent(
                declaredLength:
                    1048577);

        var handler =
            new StubHttpMessageHandler(
                _ =>
                    new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content =
                            content
                    });

        using var client =
            CreateClient(handler);

        var provider =
            CreateProvider(
                client,
                maxResponseBytes:
                    1048576);

        AiProviderHealthResult result =
            await provider.CheckHealthAsync(
                ["model-fast"]);

        Assert.Equal(
            AiProviderHealthStatus.Unavailable,
            result.Status);

        Assert.Equal(
            "OLLAMA_RESPONSE_TOO_LARGE",
            result.ErrorCode);

        Assert.False(
            content.WasRead);
    }

    private static OllamaProvider CreateProvider(
        HttpClient client,
        int maxResponseBytes = 1048576)
    {
        var options =
            new AiGatewayOptions
            {
                Provider =
                    "Ollama",

                Endpoint =
                    "http://example.invalid",

                Models =
                    new Dictionary<string, string>
                    {
                        ["CodingFast"] =
                            "model-fast",

                        ["CodingQuality"] =
                            "model-quality",

                        ["General"] =
                            "model-general"
                    },

                MaxConcurrentInferences =
                    1,

                ConnectionTimeout =
                    TimeSpan.FromSeconds(1),

                InferenceTimeout =
                    TimeSpan.FromSeconds(10),

                QueueWaitTimeout =
                    TimeSpan.FromSeconds(1),

                MaxOutputTokens =
                    256,

                MaxInputCharacters =
                    65536,

                MaxResponseBytes =
                    maxResponseBytes
            };

        return new OllamaProvider(
            client,
            options);
    }

    private static HttpClient CreateClient(
        HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress =
                new Uri(
                    "http://example.invalid/")
        };
    }

    private static AiRequest CreateRequest()
    {
        return new AiRequest
        {
            Model =
                AiLogicalModel.CodingFast,

            SystemInstructions =
                "system instructions",

            UserContent =
                "user content",

            CorrelationId =
                "ollama-provider-test",

            Generation =
                new AiGenerationOptions
                {
                    MaxOutputTokens =
                        32,

                    Temperature =
                        0
                }
        };
    }

    private static HttpResponseMessage JsonResponse(
        string json)
    {
        return new HttpResponseMessage(
            HttpStatusCode.OK)
        {
            Content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json")
        };
    }

    private sealed class ThrowIfReadContent :
        HttpContent
    {
        private readonly long declaredLength;

        public ThrowIfReadContent(
            long declaredLength)
        {
            this.declaredLength =
                declaredLength;

            Headers.ContentLength =
                declaredLength;
        }

        public bool WasRead
        {
            get;
            private set;
        }

        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context)
        {
            WasRead =
                true;

            throw new InvalidOperationException(
                "Response body must not be buffered or read.");
        }

        protected override bool TryComputeLength(
            out long length)
        {
            length =
                declaredLength;

            return true;
        }
    }

    private sealed class StubHttpMessageHandler :
        HttpMessageHandler
    {
        private readonly Func<
            HttpRequestMessage,
            Task<HttpResponseMessage>> handler;

        public StubHttpMessageHandler(
            Func<
                HttpRequestMessage,
                HttpResponseMessage> handler)
            : this(
                request =>
                    Task.FromResult(
                        handler(request)))
        {
        }

        public StubHttpMessageHandler(
            Func<
                HttpRequestMessage,
                Task<HttpResponseMessage>> handler)
        {
            this.handler =
                handler;
        }

        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            return handler(request);
        }
    }

    private sealed class ThrowingHttpMessageHandler :
        HttpMessageHandler
    {
        private readonly Exception exception;

        public ThrowingHttpMessageHandler(
            Exception exception)
        {
            this.exception =
                exception;
        }

        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            return Task.FromException<
                HttpResponseMessage>(
                exception);
        }
    }
}
