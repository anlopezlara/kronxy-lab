using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.ProjectTasks.GetProjectTask;
public sealed record GetProjectTasksByUserQuery(Guid UserId)
    : IQuery<IReadOnlyList<ProjectTaskResponse>>;
