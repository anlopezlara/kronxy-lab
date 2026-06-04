using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.ProjectTasks.GetProjectTask;
public sealed record GetProjectTaskQuery(Guid ProjectTaskId) : IQuery<ProjectTaskResponse>;
