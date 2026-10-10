using System.Diagnostics;
using Kronxy.Application.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Kronxy.Infrastructure.AI;

public sealed class AiGateway : IAiGateway, IDisposable
{
    private const int MaxCorrelationIdCharacters =
        128;

    private readonly IAiProvider provider;
    private readonly AiGatewayOptions options;
    private readonly AiModelCatalog modelCatalog;
    private readonly AiStructuredOutputValidator structuredOutputValidator;
    private readonly ILogger<AiGateway> logger;
    private readonly SemaphoreSlim concurrencyGate;

    private bool disposed;

    public AiGateway(
        IAiProvider provider,
        AiGatewayOptions options,
        AiModelCatalog modelCatalog,
        AiStructuredOutputValidator structuredOutputValidator,
        ILogger<AiGateway> logger)
    {
        this.provider =
            provider ??
            throw new ArgumentNullException(
                nameof(provider));

        this.options =
            options ??
            throw new ArgumentNullException(
                nameof(options));

        this.modelCatalog =
            modelCatalog ??
            throw new ArgumentNullException(
                nameof(modelCatalog));

        this.structuredOutputValidator =
            structuredOutputValidator ??
            throw new ArgumentNullException(
                nameof(structuredOutputValidator));

        this.logger =
            logger ??
            throw new ArgumentNullException(
                nameof(logger));

        options.Validate();

        concurrencyGate =
            new SemaphoreSlim(
                options.MaxConcurrentInferences,
                options.MaxConcurrentInferences);
    }

    public async Task<AiResponse> GenerateAsync(
        AiRequest request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(
            disposed,
            this);

        AiResponse? rejection =
            ValidateRequest(
                request);

        if (rejection is not null)
        {
            return rejection;
        }

        if (!modelCatalog.TryResolve(
                request.Model,
                out string physicalModel))
        {
            return Failure(
                request,
                AiOperationStatus.Rejected,
                "AI_MODEL_NOT_ALLOWED");
        }

        bool acquired;

        try
        {
            acquired =
                await concurrencyGate
                    .WaitAsync(
                        options.QueueWaitTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return Failure(
                request,
                AiOperationStatus.Cancelled,
                "AI_CANCELLED",
                AiTerminationReason.Cancelled);
        }

        if (!acquired)
        {
            return Failure(
                request,
                AiOperationStatus.Rejected,
                "AI_BACKPRESSURE");
        }

        try
        {
            TimeSpan effectiveInferenceTimeout =
                request.InferenceTimeout ??
                options.InferenceTimeout;

            TimeSpan maximumInferenceTimeout =
                options.PlanningInferenceTimeout >
                    options.DeveloperInferenceTimeout
                    ? options.PlanningInferenceTimeout
                    : options.DeveloperInferenceTimeout;

            if (effectiveInferenceTimeout <= TimeSpan.Zero ||
                effectiveInferenceTimeout == Timeout.InfiniteTimeSpan ||
                effectiveInferenceTimeout >
                    maximumInferenceTimeout)
            {
                return Failure(
                    request,
                    AiOperationStatus.Rejected,
                    "AI_INFERENCE_TIMEOUT_INVALID");
            }

            using var inferenceTimeout =
                new CancellationTokenSource(
                    effectiveInferenceTimeout);

            using var linkedToken =
                CancellationTokenSource
                    .CreateLinkedTokenSource(
                        cancellationToken,
                        inferenceTimeout.Token);

            Stopwatch stopwatch =
                Stopwatch.StartNew();

            try
            {
                AiRequest effectiveRequest =
                    request.Generation.MaxOutputTokens is null
                        ? request with
                        {
                            Generation =
                                request.Generation with
                                {
                                    MaxOutputTokens =
                                        options.MaxOutputTokens
                                }
                        }
                        : request;

                AiResponse response =
                    await provider
                        .GenerateAsync(
                            effectiveRequest,
                            physicalModel,
                            linkedToken.Token)
                        .ConfigureAwait(false);

                stopwatch.Stop();

                AiResponse normalized =
                    response with
                    {
                        LogicalModel =
                            request.Model.ToString(),

                        PhysicalModel =
                            physicalModel,

                        Duration =
                            response.Duration == TimeSpan.Zero
                                ? stopwatch.Elapsed
                                : response.Duration
                    };

                if (normalized.IsSuccess &&
                    normalized.TerminationReason == AiTerminationReason.Length)
                {
                    normalized = normalized with
                    {
                        Status = AiOperationStatus.InvalidResponse,
                        ErrorCode = "AI_OUTPUT_TRUNCATED"
                    };
                }
                else if (normalized.IsSuccess &&
                    request.StructuredOutput is not null)
                {
                    if (!structuredOutputValidator
                        .TryValidate(
                            request.StructuredOutput
                                .Schema,
                            normalized.Content,
                            out string validationError))
                    {
                        normalized =
                            normalized with
                            {
                                Status =
                                    AiOperationStatus.InvalidResponse,

                                TerminationReason =
                                    AiTerminationReason.Error,

                                ErrorCode =
                                    validationError
                            };
                    }
                }

                LogCompletion(
                    request,
                    normalized);

                return normalized;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();

                return Failure(
                    request,
                    AiOperationStatus.Cancelled,
                    "AI_CANCELLED",
                    AiTerminationReason.Cancelled,
                    physicalModel,
                    stopwatch.Elapsed);
            }
            catch (OperationCanceledException)
                when (inferenceTimeout.IsCancellationRequested)
            {
                stopwatch.Stop();

                return Failure(
                    request,
                    AiOperationStatus.TimedOut,
                    "AI_INFERENCE_TIMEOUT",
                    AiTerminationReason.Error,
                    physicalModel,
                    stopwatch.Elapsed);
            }
        }
        finally
        {
            concurrencyGate.Release();
        }
    }

    public Task<AiProviderHealthResult> CheckHealthAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(
            disposed,
            this);

        return provider.CheckHealthAsync(
            modelCatalog.RequiredPhysicalModels,
            cancellationToken);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        concurrencyGate.Dispose();
        disposed = true;
    }

    private AiResponse? ValidateRequest(
        AiRequest? request)
    {
        if (request is null)
        {
            return new AiResponse
            {
                Status =
                    AiOperationStatus.Rejected,

                Provider =
                    options.Provider,

                ErrorCode =
                    "AI_REQUEST_REQUIRED",

                TerminationReason =
                    AiTerminationReason.Error
            };
        }

        if (string.IsNullOrWhiteSpace(
                request.SystemInstructions))
        {
            return Failure(
                request,
                AiOperationStatus.Rejected,
                "AI_SYSTEM_INSTRUCTIONS_REQUIRED");
        }

        if (string.IsNullOrWhiteSpace(
                request.UserContent))
        {
            return Failure(
                request,
                AiOperationStatus.Rejected,
                "AI_USER_CONTENT_REQUIRED");
        }

        if (string.IsNullOrWhiteSpace(
                request.CorrelationId))
        {
            return Failure(
                request,
                AiOperationStatus.Rejected,
                "AI_CORRELATION_ID_REQUIRED");
        }

        if (request.CorrelationId.Length >
            MaxCorrelationIdCharacters)
        {
            return Failure(
                request,
                AiOperationStatus.Rejected,
                "AI_CORRELATION_ID_TOO_LONG");
        }

        if (request.CorrelationId.IndexOfAny(
                ['\0', '\r', '\n']) >= 0)
        {
            return Failure(
                request,
                AiOperationStatus.Rejected,
                "AI_CORRELATION_ID_INVALID");
        }

        long inputCharacters =
            (long)request.SystemInstructions.Length +
            request.UserContent.Length;

        if (request.StructuredOutput is not null &&
            request.StructuredOutput.Schema.ValueKind !=
                JsonValueKind.Undefined)
        {
            inputCharacters +=
                request.StructuredOutput
                    .Schema
                    .GetRawText()
                    .Length;
        }

        if (inputCharacters >
            options.MaxInputCharacters)
        {
            return Failure(
                request,
                AiOperationStatus.Rejected,
                "AI_INPUT_LIMIT_EXCEEDED");
        }

        int? requestedLimit =
            request.Generation.MaxOutputTokens;

        if (requestedLimit is <= 0)
        {
            return Failure(
                request,
                AiOperationStatus.Rejected,
                "AI_OUTPUT_LIMIT_INVALID");
        }

        if (requestedLimit > Math.Max(
                options.MaxOutputTokens,
                options.DeveloperMaxOutputTokens))
        {
            return Failure(
                request,
                AiOperationStatus.Rejected,
                "AI_OUTPUT_LIMIT_EXCEEDED");
        }

        int? requestedContextWindow =
            request.Generation.ContextWindowTokens;
        if (requestedContextWindow is <= 0 ||
            requestedContextWindow > options.DeveloperContextWindowTokens)
        {
            return Failure(
                request,
                AiOperationStatus.Rejected,
                "AI_CONTEXT_WINDOW_LIMIT_EXCEEDED");
        }

        double? temperature =
            request.Generation.Temperature;

        if (temperature is < 0 or > 2)
        {
            return Failure(
                request,
                AiOperationStatus.Rejected,
                "AI_TEMPERATURE_INVALID");
        }

        return null;
    }

    private void LogCompletion(
        AiRequest request,
        AiResponse response)
    {
        logger.LogInformation(
            "AI inference completed. CorrelationId={CorrelationId} Provider={Provider} LogicalModel={LogicalModel} PhysicalModel={PhysicalModel} Status={Status} DurationMs={DurationMs} ErrorCode={ErrorCode}",
            request.CorrelationId,
            response.Provider,
            response.LogicalModel,
            response.PhysicalModel,
            response.Status,
            response.Duration.TotalMilliseconds,
            response.ErrorCode);
    }

    private AiResponse Failure(
        AiRequest request,
        AiOperationStatus status,
        string errorCode,
        AiTerminationReason terminationReason =
            AiTerminationReason.Error,
        string physicalModel = "",
        TimeSpan duration = default)
    {
        var response =
            new AiResponse
            {
                Status =
                    status,

                Provider =
                    options.Provider,

                LogicalModel =
                    request.Model.ToString(),

                PhysicalModel =
                    physicalModel,

                Duration =
                    duration,

                TerminationReason =
                    terminationReason,

                ErrorCode =
                    errorCode
            };

        LogCompletion(
            request,
            response);

        return response;
    }
}
