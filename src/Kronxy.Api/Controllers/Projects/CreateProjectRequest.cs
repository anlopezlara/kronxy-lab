namespace Kronxy.Api.Controllers.Projects;

public sealed record CreateProjectRequest(
    string Code,
    string Name,
    string? Description,
    Guid OwnerId,
    Guid ProjectTypeId);