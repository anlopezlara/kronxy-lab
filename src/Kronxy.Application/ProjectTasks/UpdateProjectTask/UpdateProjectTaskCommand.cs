using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.ProjectTasks.UpdateProjectTask;
public sealed record UpdateProjectTaskCommand(
    Guid ProjectTaskId,
    Guid AssignedUserId,
    string Title,
    string? Description) : ICommand;
