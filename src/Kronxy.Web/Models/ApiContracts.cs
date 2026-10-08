namespace Kronxy.Web.Models;

public sealed record HealthDto(string Status, string Api, string Database);

public sealed record JobPageDto(
    IReadOnlyList<JobSummaryDto> Items, int Page, int PageSize, int TotalItems, int TotalPages);

public sealed record JobSummaryDto(
    Guid JobId, string ExternalId, string State, int AttemptCount,
    bool HumanReviewRequired, string? LastErrorCode, string? LastErrorMessage,
    DateTime CreatedOnUtc, DateTime UpdatedOnUtc, Guid RunId);

public sealed record JobDetailDto
{
    public Guid Id { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Request { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string? ResumeState { get; init; }
    public int AttemptCount { get; init; }
    public Guid RunId { get; init; }
    public bool HumanReviewRequired { get; init; }
    public string? BaseRepositoryHead { get; init; }
    public DateTime CreatedOnUtc { get; init; }
    public DateTime UpdatedOnUtc { get; init; }
    public DateTime? CompletedOnUtc { get; init; }
    public string? LastErrorCode { get; init; }
    public string? LastErrorMessage { get; init; }
    public long Version { get; init; }
}

public sealed record JobHistoryDto(
    int Sequence, DateTime Timestamp, string FromState, string ToState,
    string Actor, string CorrelationId, string Reason, int? AttemptCount);

public sealed record ArtifactDto(
    Guid ArtifactId, string ArtifactType, string ApiPath, string Sha256,
    DateTimeOffset CreatedAtUtc, string CorrelationId, long SizeBytes);

public sealed record ArtifactContentDto(
    byte[] Content, string ContentType, string? FileName)
{
    public bool IsText => ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
        ContentType.Contains("json", StringComparison.OrdinalIgnoreCase);
}

public sealed record LineageDto(
    string LineageType, string CorrelationId, Guid RunId, int AttemptCount,
    bool SourceMutationPresent, bool ObservedChangesAvailable,
    string? BuildStatus, string? TestStatus, string? ReviewerStatus);

public sealed record AllowedActionDto(string Action, bool Allowed, string? ReasonCode);

public sealed record ApiErrorDto(string? Code, string? Message);
