using Kronxy.Web.Models;

namespace Kronxy.Web.Clients;

public interface IKronxyApiClient
{
    Task<HealthDto> GetHealthAsync(CancellationToken cancellationToken = default);
    Task<JobPageDto> GetJobsAsync(int page, int pageSize, string? externalId = null,
        string? state = null, CancellationToken cancellationToken = default);
    Task<JobDetailDto> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobHistoryDto>> GetJobHistoryAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArtifactDto>> GetJobArtifactsAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<ArtifactContentDto> GetArtifactAsync(Guid jobId, Guid artifactId, CancellationToken cancellationToken = default);
    Task<LineageDto?> GetEffectiveLineageAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AllowedActionDto>> GetAllowedActionsAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<JobDetailDto> CreateJobAsync(string request, string? externalId,
        CancellationToken cancellationToken = default);
    Task<OperationResultDto> AdvanceJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<OperationResultDto> ResumeJobAsync(Guid jobId, string reason, CancellationToken cancellationToken = default);
    Task<OperationResultDto> RetryPendingAsync(Guid jobId, string reason, CancellationToken cancellationToken = default);
    Task<OperationResultDto> CancelJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<OperationResultDto> ApproveHumanReviewAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<OperationResultDto> RequestHumanReviewChangesAsync(Guid jobId,
        IReadOnlyList<HumanReviewCorrectionDto> corrections, CancellationToken cancellationToken = default);
    Task<OperationResultDto> ResolveArchitectureDecisionAsync(Guid jobId,
        ArchitectureDecisionInputDto decision, CancellationToken cancellationToken = default);
    Task<OperationResultDto> ApplyHumanCorrectionAsync(Guid jobId, string reason,
        IReadOnlyList<HumanFileReplacementDto> changes, CancellationToken cancellationToken = default);
    Task<OperationResultDto> SupersedeReviewerCorrectionAsync(Guid jobId,
        CancellationToken cancellationToken = default);
}
