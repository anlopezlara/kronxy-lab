using Kronxy.Application.AI;
using Kronxy.Infrastructure.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using System.Text.Json;

namespace Kronxy.ControlPlane.Tests;

public sealed class AiGatewayTests
{
    [Fact]
    public async Task GenerateAsync_maps_logical_model_and_returns_success()
    {
        var provider = new FakeAiProvider
        {
            GenerateHandler =
                (_, physicalModel, _) =>
                    Task.FromResult(
                        new AiResponse
                        {
                            Status =
                                AiOperationStatus.Success,

                            Provider =
                                "Ollama",

                            Content =
                                "ok",

                            PhysicalModel =
                                physicalModel,

                            TerminationReason =
                                AiTerminationReason.Stop
                        })
        };

        using var gateway =
            CreateGateway(provider);

        AiResponse result =
            await gateway.GenerateAsync(
                CreateRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "CodingFast",
            result.LogicalModel);
        Assert.Equal(
            "model-fast",
            result.PhysicalModel);
        Assert.Equal(
            "ok",
            result.Content);
        Assert.Equal(
            1,
            provider.GenerateCalls);
    }

    [Fact]
    public async Task GenerateAsync_rejects_unknown_logical_model()
    {
        var provider =
            new FakeAiProvider();

        using var gateway =
            CreateGateway(provider);

        AiRequest request =
            CreateRequest() with
            {
                Model =
                    (AiLogicalModel)999
            };

        AiResponse result =
            await gateway.GenerateAsync(
                request);

        Assert.Equal(
            AiOperationStatus.Rejected,
            result.Status);

        Assert.Equal(
            "AI_MODEL_NOT_ALLOWED",
            result.ErrorCode);

        Assert.Equal(
            0,
            provider.GenerateCalls);
    }

    [Fact]
    public async Task GenerateAsync_rejects_missing_system_instructions()
    {
        var provider =
            new FakeAiProvider();

        using var gateway =
            CreateGateway(provider);

        AiRequest request =
            CreateRequest() with
            {
                SystemInstructions =
                    " "
            };

        AiResponse result =
            await gateway.GenerateAsync(
                request);

        Assert.Equal(
            AiOperationStatus.Rejected,
            result.Status);

        Assert.Equal(
            "AI_SYSTEM_INSTRUCTIONS_REQUIRED",
            result.ErrorCode);

        Assert.Equal(
            0,
            provider.GenerateCalls);
    }

    [Fact]
    public async Task GenerateAsync_rejects_missing_user_content()
    {
        var provider =
            new FakeAiProvider();

        using var gateway =
            CreateGateway(provider);

        AiRequest request =
            CreateRequest() with
            {
                UserContent =
                    ""
            };

        AiResponse result =
            await gateway.GenerateAsync(
                request);

        Assert.Equal(
            AiOperationStatus.Rejected,
            result.Status);

        Assert.Equal(
            "AI_USER_CONTENT_REQUIRED",
            result.ErrorCode);

        Assert.Equal(
            0,
            provider.GenerateCalls);
    }

    [Fact]
    public async Task GenerateAsync_rejects_output_limit_above_gateway_limit()
    {
        var provider =
            new FakeAiProvider();

        using var gateway =
            CreateGateway(
                provider,
                maxOutputTokens: 100);

        AiRequest request =
            CreateRequest() with
            {
                Generation =
                    new AiGenerationOptions
                    {
                        MaxOutputTokens =
                            101
                    }
            };

        AiResponse result =
            await gateway.GenerateAsync(
                request);

        Assert.Equal(
            AiOperationStatus.Rejected,
            result.Status);

        Assert.Equal(
            "AI_OUTPUT_LIMIT_EXCEEDED",
            result.ErrorCode);

        Assert.Equal(
            0,
            provider.GenerateCalls);
    }

    [Fact]
    public async Task GenerateAsync_rejects_invalid_temperature()
    {
        var provider =
            new FakeAiProvider();

        using var gateway =
            CreateGateway(provider);

        AiRequest request =
            CreateRequest() with
            {
                Generation =
                    new AiGenerationOptions
                    {
                        MaxOutputTokens =
                            32,

                        Temperature =
                            2.1
                    }
            };

        AiResponse result =
            await gateway.GenerateAsync(
                request);

        Assert.Equal(
            AiOperationStatus.Rejected,
            result.Status);

        Assert.Equal(
            "AI_TEMPERATURE_INVALID",
            result.ErrorCode);

        Assert.Equal(
            0,
            provider.GenerateCalls);
    }

    [Fact]
    public async Task GenerateAsync_returns_cancelled_when_caller_cancels()
    {
        var provider =
            new FakeAiProvider
            {
                GenerateHandler =
                    async (_, _, cancellationToken) =>
                    {
                        await Task.Delay(
                            Timeout.InfiniteTimeSpan,
                            cancellationToken);

                        throw new InvalidOperationException();
                    }
            };

        using var gateway =
            CreateGateway(provider);

        using var cancellation =
            new CancellationTokenSource(
                TimeSpan.FromMilliseconds(50));

        AiResponse result =
            await gateway.GenerateAsync(
                CreateRequest(),
                cancellation.Token);

        Assert.Equal(
            AiOperationStatus.Cancelled,
            result.Status);

        Assert.Equal(
            "AI_CANCELLED",
            result.ErrorCode);
    }

    [Fact]
    public async Task GenerateAsync_returns_timeout_when_provider_exceeds_inference_timeout()
    {
        var provider =
            new FakeAiProvider
            {
                GenerateHandler =
                    async (_, _, cancellationToken) =>
                    {
                        await Task.Delay(
                            Timeout.InfiniteTimeSpan,
                            cancellationToken);

                        throw new InvalidOperationException();
                    }
            };

        using var gateway =
            CreateGateway(
                provider,
                inferenceTimeout:
                    TimeSpan.FromMilliseconds(50));

        AiResponse result =
            await gateway.GenerateAsync(
                CreateRequest());

        Assert.Equal(
            AiOperationStatus.TimedOut,
            result.Status);

        Assert.Equal(
            "AI_INFERENCE_TIMEOUT",
            result.ErrorCode);
    }

    [Fact]
    public async Task GenerateAsync_rejects_second_request_when_concurrency_is_saturated()
    {
        var entered =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        var release =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        var provider =
            new FakeAiProvider
            {
                GenerateHandler =
                    async (_, _, cancellationToken) =>
                    {
                        entered.TrySetResult(true);

                        await release.Task
                            .WaitAsync(
                                cancellationToken);

                        return new AiResponse
                        {
                            Status =
                                AiOperationStatus.Success,

                            Provider =
                                "Ollama",

                            Content =
                                "ok",

                            TerminationReason =
                                AiTerminationReason.Stop
                        };
                    }
            };

        using var gateway =
            CreateGateway(
                provider,
                maxConcurrent: 1,
                queueWaitTimeout:
                    TimeSpan.FromMilliseconds(50));

        Task<AiResponse> first =
            gateway.GenerateAsync(
                CreateRequest());

        await entered.Task;

        AiResponse second =
            await gateway.GenerateAsync(
                CreateRequest());

        Assert.Equal(
            AiOperationStatus.Rejected,
            second.Status);

        Assert.Equal(
            "AI_BACKPRESSURE",
            second.ErrorCode);

        release.TrySetResult(true);

        AiResponse firstResult =
            await first;

        Assert.True(
            firstResult.IsSuccess);
    }

    [Fact]
    public async Task GenerateAsync_applies_gateway_output_limit_when_request_limit_is_null()
    {
        AiRequest? capturedRequest =
            null;

        var provider =
            new FakeAiProvider
            {
                GenerateHandler =
                    (
                        request,
                        _,
                        _) =>
                    {
                        capturedRequest =
                            request;

                        return Task.FromResult(
                            new AiResponse
                            {
                                Status =
                                    AiOperationStatus.Success,

                                Content =
                                    "ok",

                                Provider =
                                    "Ollama"
                            });
                    }
            };

        using var gateway =
            CreateGateway(
                provider,
                maxOutputTokens: 77);

        AiRequest request =
            CreateRequest() with
            {
                Generation =
                    new AiGenerationOptions
                    {
                        MaxOutputTokens =
                            null,

                        Temperature =
                            0
                    }
            };

        AiResponse result =
            await gateway.GenerateAsync(
                request);

        Assert.True(
            result.IsSuccess);

        Assert.NotNull(
            capturedRequest);

        Assert.Equal(
            77,
            capturedRequest!
                .Generation
                .MaxOutputTokens);
    }

    [Fact]
    public async Task GenerateAsync_rejects_input_above_combined_character_limit()
    {
        var provider =
            new FakeAiProvider();

        using var gateway =
            CreateGateway(
                provider,
                maxInputCharacters: 6);

        AiRequest request =
            CreateRequest() with
            {
                SystemInstructions =
                    "abcd",

                UserContent =
                    "xyz"
            };

        AiResponse result =
            await gateway.GenerateAsync(
                request);

        Assert.Equal(
            AiOperationStatus.Rejected,
            result.Status);

        Assert.Equal(
            "AI_INPUT_LIMIT_EXCEEDED",
            result.ErrorCode);

        Assert.Equal(
            0,
            provider.GenerateCalls);
    }

    [Fact]
    public async Task GenerateAsync_accepts_structured_schema_within_input_limit()
    {
        var provider =
            new FakeAiProvider
            {
                GenerateHandler =
                    (_, physicalModel, _) =>
                        Task.FromResult(
                            new AiResponse
                            {
                                Status =
                                    AiOperationStatus.Success,

                                Provider =
                                    "Ollama",

                                PhysicalModel =
                                    physicalModel,

                                Content =
                                    "{\"status\":\"ok\"}",

                                TerminationReason =
                                    AiTerminationReason.Stop
                            })
            };

        using var gateway =
            CreateGateway(
                provider,
                maxInputCharacters:
                    512);

        JsonElement schema =
            JsonSerializer.Deserialize<JsonElement>(
                """
                {
                  "type": "object",
                  "properties": {
                    "status": {
                      "type": "string",
                      "const": "ok"
                    }
                  },
                  "required": [
                    "status"
                  ],
                  "additionalProperties": false
                }
                """);

        AiRequest request =
            CreateRequest() with
            {
                SystemInstructions =
                    "s",

                UserContent =
                    "u",

                StructuredOutput =
                    new AiStructuredOutput
                    {
                        Schema =
                            schema
                    }
            };

        AiResponse result =
            await gateway.GenerateAsync(
                request);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            1,
            provider.GenerateCalls);
    }

    [Fact]
    public async Task GenerateAsync_rejects_when_structured_schema_exceeds_input_limit()
    {
        var provider =
            new FakeAiProvider();

        using var gateway =
            CreateGateway(
                provider,
                maxInputCharacters:
                    120);

        string largePropertyName =
            new string(
                'x',
                180);

        JsonElement schema =
            JsonSerializer.Deserialize<JsonElement>(
                $$"""
                {
                  "type": "object",
                  "properties": {
                    "{{largePropertyName}}": {
                      "type": "string"
                    }
                  },
                  "additionalProperties": false
                }
                """);

        AiRequest request =
            CreateRequest() with
            {
                SystemInstructions =
                    "s",

                UserContent =
                    "u",

                StructuredOutput =
                    new AiStructuredOutput
                    {
                        Schema =
                            schema
                    }
            };

        AiResponse result =
            await gateway.GenerateAsync(
                request);

        Assert.Equal(
            AiOperationStatus.Rejected,
            result.Status);

        Assert.Equal(
            "AI_INPUT_LIMIT_EXCEEDED",
            result.ErrorCode);

        Assert.Equal(
            0,
            provider.GenerateCalls);
    }

    [Fact]
    public async Task GenerateAsync_rejects_missing_correlation_id()
    {
        var provider =
            new FakeAiProvider();

        using var gateway =
            CreateGateway(provider);

        AiRequest request =
            CreateRequest() with
            {
                CorrelationId =
                    " "
            };

        AiResponse result =
            await gateway.GenerateAsync(
                request);

        Assert.Equal(
            AiOperationStatus.Rejected,
            result.Status);

        Assert.Equal(
            "AI_CORRELATION_ID_REQUIRED",
            result.ErrorCode);

        Assert.Equal(
            0,
            provider.GenerateCalls);
    }

    [Fact]
    public async Task GenerateAsync_rejects_correlation_id_above_limit()
    {
        var provider =
            new FakeAiProvider();

        using var gateway =
            CreateGateway(provider);

        AiRequest request =
            CreateRequest() with
            {
                CorrelationId =
                    new string(
                        'x',
                        129)
            };

        AiResponse result =
            await gateway.GenerateAsync(
                request);

        Assert.Equal(
            AiOperationStatus.Rejected,
            result.Status);

        Assert.Equal(
            "AI_CORRELATION_ID_TOO_LONG",
            result.ErrorCode);

        Assert.Equal(
            0,
            provider.GenerateCalls);
    }

    [Fact]
    public async Task GenerateAsync_rejects_correlation_id_with_control_characters()
    {
        var provider =
            new FakeAiProvider();

        using var gateway =
            CreateGateway(provider);

        AiRequest request =
            CreateRequest() with
            {
                CorrelationId =
                    "abc\r\ninjected"
            };

        AiResponse result =
            await gateway.GenerateAsync(
                request);

        Assert.Equal(
            AiOperationStatus.Rejected,
            result.Status);

        Assert.Equal(
            "AI_CORRELATION_ID_INVALID",
            result.ErrorCode);

        Assert.Equal(
            0,
            provider.GenerateCalls);
    }

    [Fact]
    public async Task CheckHealthAsync_passes_required_models_to_provider()
    {
        var provider =
            new FakeAiProvider();

        using var gateway =
            CreateGateway(provider);

        AiProviderHealthResult result =
            await gateway.CheckHealthAsync();

        Assert.True(
            result.IsAvailable);

        Assert.Equal(
            3,
            provider.LastHealthModels.Count);

        Assert.Contains(
            "model-fast",
            provider.LastHealthModels);

        Assert.Contains(
            "model-quality",
            provider.LastHealthModels);

        Assert.Contains(
            "model-general",
            provider.LastHealthModels);
    }

    [Fact]
    public async Task GenerateAsync_logs_metadata_without_prompt_or_response_content()
    {
        const string SecretSystem =
            "SECRET_SYSTEM_7F913";

        const string SecretUser =
            "SECRET_USER_A18BC";

        const string SecretResponse =
            "SECRET_RESPONSE_D29EF";

        var provider =
            new FakeAiProvider
            {
                GenerateHandler =
                    (_, physicalModel, _) =>
                        Task.FromResult(
                            new AiResponse
                            {
                                Status =
                                    AiOperationStatus.Success,

                                Provider =
                                    "Ollama",

                                PhysicalModel =
                                    physicalModel,

                                Content =
                                    SecretResponse,

                                TerminationReason =
                                    AiTerminationReason.Stop
                            })
            };

        var logger =
            new CapturingLogger<AiGateway>();

        AiGateway gateway =
            CreateGateway(
                provider,
                logger: logger);

        AiRequest request =
            CreateRequest() with
            {
                SystemInstructions =
                    SecretSystem,

                UserContent =
                    SecretUser,

                CorrelationId =
                    "safe-log-correlation"
            };

        AiResponse result =
            await gateway.GenerateAsync(
                request);

        Assert.True(
            result.IsSuccess);

        string logs =
            string.Join(
                Environment.NewLine,
                logger.Messages);

        Assert.Contains(
            "safe-log-correlation",
            logs,
            StringComparison.Ordinal);

        Assert.Contains(
            "Ollama",
            logs,
            StringComparison.Ordinal);

        Assert.Contains(
            "CodingFast",
            logs,
            StringComparison.Ordinal);

        Assert.Contains(
            "model-fast",
            logs,
            StringComparison.Ordinal);

        Assert.Contains(
            "Success",
            logs,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            SecretSystem,
            logs,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            SecretUser,
            logs,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            SecretResponse,
            logs,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_logs_error_metadata_without_request_content()
    {
        const string SecretSystem =
            "SECRET_SYSTEM_FAIL_1122";

        const string SecretUser =
            "SECRET_USER_FAIL_3344";

        var provider =
            new FakeAiProvider
            {
                GenerateHandler =
                    (_, physicalModel, _) =>
                        Task.FromResult(
                            new AiResponse
                            {
                                Status =
                                    AiOperationStatus.ProviderError,

                                Provider =
                                    "Ollama",

                                PhysicalModel =
                                    physicalModel,

                                ErrorCode =
                                    "OLLAMA_HTTP_500",

                                TerminationReason =
                                    AiTerminationReason.Error
                            })
            };

        var logger =
            new CapturingLogger<AiGateway>();

        AiGateway gateway =
            CreateGateway(
                provider,
                logger: logger);

        AiRequest request =
            CreateRequest() with
            {
                SystemInstructions =
                    SecretSystem,

                UserContent =
                    SecretUser,

                CorrelationId =
                    "safe-log-error"
            };

        AiResponse result =
            await gateway.GenerateAsync(
                request);

        Assert.Equal(
            AiOperationStatus.ProviderError,
            result.Status);

        string logs =
            string.Join(
                Environment.NewLine,
                logger.Messages);

        Assert.Contains(
            "safe-log-error",
            logs,
            StringComparison.Ordinal);

        Assert.Contains(
            "OLLAMA_HTTP_500",
            logs,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            SecretSystem,
            logs,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            SecretUser,
            logs,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_logs_structured_validation_failure_without_content()
    {
        const string SecretSystem =
            "SECRET_STRUCTURED_SYSTEM_8821";

        const string SecretUser =
            "SECRET_STRUCTURED_USER_9932";

        const string SecretResponse =
            "{\"status\":\"SECRET_STRUCTURED_RESPONSE_1143\"}";

        var provider =
            new FakeAiProvider
            {
                GenerateHandler =
                    (_, physicalModel, _) =>
                        Task.FromResult(
                            new AiResponse
                            {
                                Status =
                                    AiOperationStatus.Success,

                                Provider =
                                    "Ollama",

                                PhysicalModel =
                                    physicalModel,

                                Content =
                                    SecretResponse,

                                TerminationReason =
                                    AiTerminationReason.Stop
                            })
            };

        var logger =
            new CapturingLogger<AiGateway>();

        using AiGateway gateway =
            CreateGateway(
                provider,
                logger: logger);

        AiRequest request =
            CreateRequest() with
            {
                SystemInstructions =
                    SecretSystem,

                UserContent =
                    SecretUser,

                CorrelationId =
                    "safe-log-structured",

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
                                          "type": "string",
                                          "const": "ok"
                                        }
                                      },
                                      "required": [
                                        "status"
                                      ],
                                      "additionalProperties": false
                                    }
                                    """)
                    }
            };

        AiResponse result =
            await gateway.GenerateAsync(
                request);

        Assert.Equal(
            AiOperationStatus.InvalidResponse,
            result.Status);

        string logs =
            string.Join(
                Environment.NewLine,
                logger.Messages);

        Assert.Contains(
            "safe-log-structured",
            logs,
            StringComparison.Ordinal);

        Assert.Contains(
            "InvalidResponse",
            logs,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            SecretSystem,
            logs,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            SecretUser,
            logs,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "SECRET_STRUCTURED_RESPONSE_1143",
            logs,
            StringComparison.Ordinal);
    }

    private static AiGateway CreateGateway(
        FakeAiProvider provider,
        int maxConcurrent = 1,
        int maxOutputTokens = 256,
        int maxInputCharacters = 65536,
        int maxResponseBytes = 1048576,
        TimeSpan? inferenceTimeout = null,
        TimeSpan? queueWaitTimeout = null,
        ILogger<AiGateway>? logger = null)
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
                    maxConcurrent,

                ConnectionTimeout =
                    TimeSpan.FromSeconds(1),

                InferenceTimeout =
                    inferenceTimeout ??
                    TimeSpan.FromSeconds(10),

                QueueWaitTimeout =
                    queueWaitTimeout ??
                    TimeSpan.FromSeconds(1),

                MaxOutputTokens =
                    maxOutputTokens,

                MaxInputCharacters =
                    maxInputCharacters,

                MaxResponseBytes =
                    maxResponseBytes
            };

        var catalog =
            new AiModelCatalog(
                options);

        return new AiGateway(
            provider,
            options,
            catalog,
            new AiStructuredOutputValidator(),
            logger ??
                NullLogger<AiGateway>.Instance);
    }

    private static AiRequest CreateRequest()
    {
        return new AiRequest
        {
            Model =
                AiLogicalModel.CodingFast,

            SystemInstructions =
                "system",

            UserContent =
                "user",

            CorrelationId =
                "test-correlation",

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

    private sealed class CapturingLogger<T> :
        ILogger<T>
    {
        public List<string> Messages
        {
            get;
        } = [];

        public IDisposable? BeginScope<TState>(
            TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(
            LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(
                formatter(
                    state,
                    exception));
        }
    }

    private sealed class FakeAiProvider :
        IAiProvider
    {
        public int GenerateCalls
        {
            get;
            private set;
        }

        public IReadOnlyCollection<string>
            LastHealthModels
        {
            get;
            private set;
        } =
            Array.Empty<string>();

        public Func<
            AiRequest,
            string,
            CancellationToken,
            Task<AiResponse>>?
            GenerateHandler
        {
            get;
            init;
        }

        public Task<AiResponse> GenerateAsync(
            AiRequest request,
            string physicalModel,
            CancellationToken cancellationToken = default)
        {
            GenerateCalls++;

            if (GenerateHandler is not null)
            {
                return GenerateHandler(
                    request,
                    physicalModel,
                    cancellationToken);
            }

            return Task.FromResult(
                new AiResponse
                {
                    Status =
                        AiOperationStatus.Success,

                    Provider =
                        "Ollama",

                    Content =
                        "ok",

                    PhysicalModel =
                        physicalModel,

                    TerminationReason =
                        AiTerminationReason.Stop
                });
        }

        public Task<AiProviderHealthResult>
            CheckHealthAsync(
                IReadOnlyCollection<string>
                    requiredPhysicalModels,
                CancellationToken cancellationToken = default)
        {
            LastHealthModels =
                requiredPhysicalModels;

            return Task.FromResult(
                new AiProviderHealthResult(
                    AiProviderHealthStatus.Available,
                    "Ollama",
                    Array.Empty<string>(),
                    string.Empty));
        }
    }
}
