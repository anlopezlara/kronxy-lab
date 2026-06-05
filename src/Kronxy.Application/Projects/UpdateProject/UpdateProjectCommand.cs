using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Projects.UpdateProject;

public sealed record UpdateProjectCommand(
    Guid ProjectId,
    string Code,
    string Name,
    string? Description,
    Guid OwnerId,
    Guid ProjectTypeId,
    Guid ProjectStatusId,
    Guid ProjectPriorityId) : ICommand;