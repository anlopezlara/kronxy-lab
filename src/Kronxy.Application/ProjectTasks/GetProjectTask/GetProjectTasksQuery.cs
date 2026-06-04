using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.ProjectTasks.GetProjectTask;
public sealed record GetProjectTasksQuery : IQuery<IReadOnlyList<ProjectTaskResponse>>;
