using System.Text.Json;

namespace Kronxy.Application.AI;

public enum AiLogicalModel
{
    CodingFast = 10,
    CodingQuality = 20,
    General = 30
}

public enum AiOperationStatus
{
    Success = 0,
    Rejected = 10,
    TimedOut = 20,
    Cancelled = 30,
    ProviderUnavailable = 40,
    ProviderError = 50,
    InvalidResponse = 60
}

public enum AiTerminationReason
{
    Unknown = 0,
    Stop = 10,
    Length = 20,
    Cancelled = 30,
    Error = 40
}

public sealed record AiGenerationOptions
{
    public int? MaxOutputTokens { get; init; }

    public double? Temperature { get; init; }
}

public sealed record AiStructuredOutput
{
    public required JsonElement Schema { get; init; }
}

public sealed record AiRequest
{
    public required AiLogicalModel Model { get; init; }

    public required string SystemInstructions { get; init; }

    public required string UserContent { get; init; }

    public string CorrelationId { get; init; } =
        string.Empty;

    public AiGenerationOptions Generation { get; init; } =
        new();

    public AiStructuredOutput? StructuredOutput { get; init; }

    public TimeSpan? InferenceTimeout { get; init; }
}

public sealed record AiUsage(
    long? PromptTokens,
    long? CompletionTokens);

public sealed record AiProviderResponseMetadata
{
    public bool? Done { get; init; }

    public string? DoneReason { get; init; }

    public long? PromptEvalCount { get; init; }

    public long? EvalCount { get; init; }

    public long? TotalDurationNanoseconds { get; init; }

    public long? PromptEvalDurationNanoseconds { get; init; }

    public long? EvalDurationNanoseconds { get; init; }
}

public sealed record AiResponse
{
    public required AiOperationStatus Status { get; init; }

    public string Content { get; init; } =
        string.Empty;

    public string Provider { get; init; } =
        string.Empty;

    public string LogicalModel { get; init; } =
        string.Empty;

    public string PhysicalModel { get; init; } =
        string.Empty;

    public TimeSpan Duration { get; init; }

    public AiTerminationReason TerminationReason { get; init; }

    public AiUsage Usage { get; init; } =
        new(null, null);

    public AiProviderResponseMetadata? ProviderMetadata { get; init; }

    public string ErrorCode { get; init; } =
        string.Empty;

    public bool IsSuccess =>
        Status == AiOperationStatus.Success;
}

public enum AiProviderHealthStatus
{
    Available = 0,
    Unavailable = 10,
    Misconfigured = 20
}

public sealed record AiProviderHealthResult(
    AiProviderHealthStatus Status,
    string Provider,
    IReadOnlyList<string> MissingModels,
    string ErrorCode)
{
    public bool IsAvailable =>
        Status == AiProviderHealthStatus.Available;
}

public interface IAiProvider
{
    Task<AiResponse> GenerateAsync(
        AiRequest request,
        string physicalModel,
        CancellationToken cancellationToken = default);

    Task<AiProviderHealthResult> CheckHealthAsync(
        IReadOnlyCollection<string> requiredPhysicalModels,
        CancellationToken cancellationToken = default);
}

public interface IAiGateway
{
    Task<AiResponse> GenerateAsync(
        AiRequest request,
        CancellationToken cancellationToken = default);

    Task<AiProviderHealthResult> CheckHealthAsync(
        CancellationToken cancellationToken = default);
}
