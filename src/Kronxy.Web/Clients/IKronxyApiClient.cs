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
}
