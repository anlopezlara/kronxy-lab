namespace Kronxy.Api.Controllers.Projects;

public sealed record UpdateProjectRequest(
    string Code,
    string Name,
    string? Description,
    Guid OwnerId,
    Guid ProjectTypeId,
    Guid ProjectStatusId,
    Guid ProjectPriorityId);