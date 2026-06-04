using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.ProjectTasks.DeactivateProjectTask;
public sealed record DeactivateProjectTaskCommand(Guid ProjectTaskId) : ICommand;
