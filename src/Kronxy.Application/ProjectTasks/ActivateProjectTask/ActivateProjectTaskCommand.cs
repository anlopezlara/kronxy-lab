using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.ProjectTasks.ActivateProjectTask;
public sealed record ActivateProjectTaskCommand(Guid ProjectTaskId) : ICommand;
