using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.Projects.GetProject;
public sealed record GetProjectsQuery
    : IQuery<IReadOnlyList<ProjectResponse>>;
