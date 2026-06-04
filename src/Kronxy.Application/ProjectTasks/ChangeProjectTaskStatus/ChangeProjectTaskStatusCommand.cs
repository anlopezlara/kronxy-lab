using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.ProjectTasks;
namespace Kronxy.Application.ProjectTasks.ChangeProjectTaskStatus;
public sealed record ChangeProjectTaskStatusCommand(
    Guid ProjectTaskId,
    ProjectTaskStatus Status) : ICommand;
