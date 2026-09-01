using System.Text;
using System.Text.Json;
using Kronxy.Application.AI;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Context;
using Kronxy.Application.Execution;
using Kronxy.Infrastructure.AI;
using Kronxy.Infrastructure.Artifacts;
using Microsoft.Extensions.Logging;

namespace Kronxy.Infrastructure.Execution;

public sealed class PlanningExecutionService :
    IPlanningExecutionService
{
    private const string SystemInstructions =
        "You are the KRONXY planning model. " +
        "Analyze only the supplied job request and authorized repository context. " +
        "Do not execute commands, access files, select paths, call tools, or modify code. " +
        "Return a concise implementation plan grounded only in the supplied context.";

    private readonly IArtifactReader artifactReader;
    private readonly IContextAiInputBuilder contextBuilder;
    private readonly IAiGateway aiGateway;
    private readonly IArtifactStore artifactStore;
    private readonly ArtifactStoreOptions artifactOptions;
    private readonly AiGatewayOptions aiOptions;
    private readonly ILogger<PlanningExecutionService>? logger;

    public PlanningExecutionService(
        IArtifactReader artifactReader,
        IContextAiInputBuilder contextBuilder,
        IAiGateway aiGateway,
        IArtifactStore artifactStore,
        ArtifactStoreOptions artifactOptions,
        AiGatewayOptions aiOptions,
        ILogger<PlanningExecutionService>? logger = null)
    {
        this.artifactReader =
            artifactReader ??
            throw new ArgumentNullException(
                nameof(artifactReader));

        this.contextBuilder =
            contextBuilder ??
            throw new ArgumentNullException(
                nameof(contextBuilder));

        this.aiGateway =
            aiGateway ??
            throw new ArgumentNullException(
                nameof(aiGateway));

        this.artifactStore =
            artifactStore ??
            throw new ArgumentNullException(
                nameof(artifactStore));

        this.artifactOptions =
            artifactOptions ??
            throw new ArgumentNullException(
                nameof(artifactOptions));

        this.aiOptions =
            aiOptions ??
            throw new ArgumentNullException(
                nameof(aiOptions));

        this.logger = logger;

        this.artifactOptions.Validate();
        this.aiOptions.Validate();
    }

    public async Task<PlanningExecutionResult> ExecuteAsync(
        PlanningExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidRequest(request))
        {
            return Failure(
                PlanningExecutionFailureKind.InvalidRequest,
                "PLANNING_INVALID_REQUEST");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            ArtifactReadResult artifact =
                await artifactReader.ReadAsync(
                    new ArtifactReadRequest
                    {
                        JobId = request.JobId,
                        RunId = request.RunId,
                        ArtifactType =
                            ArtifactType.ContextPackage,
                        MaxBytes =
                            artifactOptions.MaxArtifactBytes,
                        CorrelationId =
                            request.CorrelationId
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (!artifact.IsSuccess)
            {
                return Failure(
                    MapArtifactReadFailure(
                        artifact.FailureKind),
                    artifact.ErrorCode);
            }

            string prefix =
                "JOB REQUEST:\n" +
                request.JobRequest +
                "\n\nAUTHORIZED REPOSITORY CONTEXT:\n";

            int remainingCharacters =
                aiOptions.MaxInputCharacters -
                SystemInstructions.Length -
                prefix.Length;

            if (remainingCharacters <= 0)
            {
                return Failure(
                    PlanningExecutionFailureKind.ContextTooLarge,
                    "PLANNING_INPUT_LIMIT_EXCEEDED");
            }

            ContextAiInputResult context =
                await contextBuilder.BuildAsync(
                    new ContextAiInputRequest
                    {
                        PackageContent =
                            artifact.Content,
                        MaxCharacters =
                            remainingCharacters
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (!context.IsSuccess)
            {
                return Failure(
                    MapContextFailure(
                        context.FailureKind),
                    context.ErrorCode);
            }

            string userContent =
                prefix +
                context.Content;

            if ((long)SystemInstructions.Length +
                    userContent.Length >
                aiOptions.MaxInputCharacters)
            {
                return Failure(
                    PlanningExecutionFailureKind.ContextTooLarge,
                    "PLANNING_INPUT_LIMIT_EXCEEDED");
            }

            AiResponse response =
                await aiGateway.GenerateAsync(
                    new AiRequest
                    {
                        Model =
                            AiLogicalModel.CodingQuality,

                        SystemInstructions =
                            SystemInstructions,

                        UserContent =
                            userContent,

                        CorrelationId =
                            request.CorrelationId,

                        Generation =
                            new AiGenerationOptions
                            {
                                MaxOutputTokens =
                                    aiOptions.MaxOutputTokens,
                                Temperature = 0
                            }
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccess)
            {
                return Failure(
                    MapAiFailure(response.Status),
                    string.IsNullOrWhiteSpace(
                        response.ErrorCode)
                        ? "PLANNING_AI_FAILED"
                        : response.ErrorCode);
            }

            byte[] responseBytes =
                JsonSerializer.SerializeToUtf8Bytes(
                    response);

            ArtifactWriteResult written =
                await artifactStore.WriteAsync(
                    new ArtifactWriteRequest
                    {
                        JobId = request.JobId,
                        RunId = request.RunId,
                        ArtifactType =
                            ArtifactType.AiResponse,
                        Content = responseBytes,
                        CorrelationId =
                            request.CorrelationId
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (!written.IsSuccess ||
                written.Artifact is null)
            {
                return Failure(
                    written.FailureKind ==
                        ArtifactStoreFailureKind.Cancelled
                        ? PlanningExecutionFailureKind.Cancelled
                        : PlanningExecutionFailureKind
                            .ArtifactWriteFailure,
                    string.IsNullOrWhiteSpace(
                        written.ErrorCode)
                        ? "PLANNING_AI_ARTIFACT_WRITE_FAILED"
                        : written.ErrorCode);
            }

            return PlanningExecutionResult.Success(
                new PlanningExecutionReport
                {
                    JobId = request.JobId,
                    RunId = request.RunId,
                    LogicalModel =
                        response.LogicalModel,
                    Provider =
                        response.Provider,
                    PhysicalModel =
                        response.PhysicalModel,
                    Duration =
                        response.Duration,
                    TerminationReason =
                        response.TerminationReason.ToString(),
                    PromptTokens =
                        response.Usage.PromptTokens,
                    CompletionTokens =
                        response.Usage.CompletionTokens
                },
                written.Artifact);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            return Failure(
                PlanningExecutionFailureKind.Cancelled,
                "PLANNING_CANCELLED");
        }
        catch (Exception exception)
            when (exception is not
                OutOfMemoryException and not
                StackOverflowException)
        {
            return Failure(
                PlanningExecutionFailureKind.InternalFailure,
                "PLANNING_INTERNAL_FAILURE");
        }
    }

    private static bool IsValidRequest(
        PlanningExecutionRequest? request) =>
        request is not null &&
        request.JobId != Guid.Empty &&
        request.RunId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(
            request.JobRequest) &&
        request.CorrelationId.IndexOfAny(
            ['\0', '\r', '\n']) < 0;

    private static PlanningExecutionFailureKind
        MapArtifactReadFailure(
            ArtifactReadFailureKind kind) =>
        kind switch
        {
            ArtifactReadFailureKind.Cancelled =>
                PlanningExecutionFailureKind.Cancelled,

            ArtifactReadFailureKind.TooLarge =>
                PlanningExecutionFailureKind.ContextTooLarge,

            ArtifactReadFailureKind.UnsafeRoot or
            ArtifactReadFailureKind.UnsafePath or
            ArtifactReadFailureKind.SymlinkEscape or
            ArtifactReadFailureKind.IntegrityFailure =>
                PlanningExecutionFailureKind
                    .ContextPackageInvalid,

            _ =>
                PlanningExecutionFailureKind
                    .ContextArtifactReadFailure
        };

    private static PlanningExecutionFailureKind
        MapContextFailure(
            ContextAiInputFailureKind kind) =>
        kind switch
        {
            ContextAiInputFailureKind.Cancelled =>
                PlanningExecutionFailureKind.Cancelled,

            ContextAiInputFailureKind.PackageTooLarge or
            ContextAiInputFailureKind.EntryTooLarge or
            ContextAiInputFailureKind.ContentTooLarge =>
                PlanningExecutionFailureKind.ContextTooLarge,

            ContextAiInputFailureKind.InvalidPackage or
            ContextAiInputFailureKind.InvalidEncoding =>
                PlanningExecutionFailureKind
                    .ContextPackageInvalid,

            _ =>
                PlanningExecutionFailureKind
                    .InternalFailure
        };

    private static PlanningExecutionFailureKind
        MapAiFailure(
            AiOperationStatus status) =>
        status switch
        {
            AiOperationStatus.Rejected =>
                PlanningExecutionFailureKind.AiRejected,

            AiOperationStatus.TimedOut =>
                PlanningExecutionFailureKind.AiTimedOut,

            AiOperationStatus.Cancelled =>
                PlanningExecutionFailureKind.AiCancelled,

            AiOperationStatus.ProviderUnavailable =>
                PlanningExecutionFailureKind.AiUnavailable,

            AiOperationStatus.ProviderError =>
                PlanningExecutionFailureKind.AiProviderError,

            AiOperationStatus.InvalidResponse =>
                PlanningExecutionFailureKind.AiInvalidResponse,

            _ =>
                PlanningExecutionFailureKind.InternalFailure
        };

    private PlanningExecutionResult Failure(
        PlanningExecutionFailureKind kind,
        string errorCode)
    {
        logger?.LogWarning(
            "Planning execution failed. FailureKind={FailureKind} ErrorCode={ErrorCode}",
            kind,
            errorCode);

        return PlanningExecutionResult.Failure(
            kind,
            errorCode);
    }
}
