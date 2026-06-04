using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.ProjectTasks.GetProjectTask;
public sealed record GetProjectTasksByProjectQuery(Guid ProjectId)
    : IQuery<IReadOnlyList<ProjectTaskResponse>>;
