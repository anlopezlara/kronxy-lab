namespace Kronxy.Api.Controllers.ProjectTasks;

public sealed record CreateProjectTaskRequest(
    Guid ProjectId,
    Guid AssignedUserId,
    string Title,
    string? Description);