using Kronxy.Application.Artifacts;
using Kronxy.Domain.Jobs;

namespace Kronxy.Application.Jobs;

public sealed record OperatorJobPage(
    IReadOnlyList<OperatorJobSummary> Items, int Page, int PageSize,
    int TotalItems, int TotalPages);

public sealed record OperatorJobSummary(
    Guid JobId, string ExternalId, string State, int AttemptCount,
    bool HumanReviewRequired, string? LastErrorCode, string? LastErrorMessage,
    DateTime CreatedOnUtc, DateTime UpdatedOnUtc, Guid RunId);

public sealed record OperatorHistoryItem(
    int Sequence, DateTime Timestamp, string FromState, string ToState,
    string Actor, string CorrelationId, string Reason, int? AttemptCount);

public sealed record OperatorArtifactItem(
    Guid ArtifactId, string ArtifactType, string ApiPath, string Sha256,
    DateTimeOffset CreatedAtUtc, string CorrelationId, long SizeBytes);

public sealed record OperatorLineage(
    string LineageType, string CorrelationId, Guid RunId, int AttemptCount,
    bool SourceMutationPresent, bool ObservedChangesAvailable,
    string? BuildStatus, string? TestStatus, string? ReviewerStatus);

public sealed record OperatorAllowedAction(string Action, bool Allowed, string? ReasonCode);

public sealed record OperatorArtifactContent(
    ArtifactRecord Metadata, ReadOnlyMemory<byte> Content, string ContentType,
    bool Inline);

public interface IOperatorJobService
{
    Task<OperatorJobPage> GetJobsAsync(int page, int pageSize, string? externalId,
        JobState? state, CancellationToken cancellationToken = default);
    Task<Job?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OperatorHistoryItem>?> GetHistoryAsync(Guid jobId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OperatorArtifactItem>?> GetArtifactsAsync(Guid jobId,
        CancellationToken cancellationToken = default);
    Task<OperatorArtifactContent?> GetArtifactAsync(Guid jobId, Guid artifactId,
        CancellationToken cancellationToken = default);
    Task<OperatorLineage?> GetEffectiveLineageAsync(Guid jobId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OperatorAllowedAction>?> GetAllowedActionsAsync(Guid jobId,
        CancellationToken cancellationToken = default);
}

public sealed record OperatorHealth(bool DatabaseUsable);

public interface IOperatorHealthService
{
    Task<OperatorHealth> CheckAsync(CancellationToken cancellationToken = default);
}
