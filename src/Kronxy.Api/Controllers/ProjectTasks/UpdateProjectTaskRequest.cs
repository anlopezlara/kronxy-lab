namespace Kronxy.Api.Controllers.ProjectTasks;

public sealed record UpdateProjectTaskRequest(
    Guid AssignedUserId,
    string Title,
    string? Description);