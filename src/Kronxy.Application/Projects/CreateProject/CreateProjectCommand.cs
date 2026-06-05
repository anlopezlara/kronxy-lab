using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Projects.CreateProject;

public sealed record CreateProjectCommand(
    string Code,
    string Name,
    string? Description,
    Guid OwnerId,
    Guid ProjectTypeId,
    Guid ProjectStatusId,
    Guid ProjectPriorityId) : ICommand<Guid>;