using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.ProjectTasks.CreateProjectTask;
public sealed record CreateProjectTaskCommand(
    Guid ProjectId,
    Guid AssignedUserId,
    string Title,
    string? Description) : ICommand<Guid>;
