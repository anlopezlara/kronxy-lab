using Kronxy.Domain.ProjectTasks;

namespace Kronxy.Api.Controllers.ProjectTasks;

public sealed record ChangeProjectTaskStatusRequest(
    ProjectTaskStatus Status);