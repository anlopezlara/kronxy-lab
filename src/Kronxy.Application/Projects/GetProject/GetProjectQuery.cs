using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.Projects.GetProject;
public sealed record GetProjectQuery(Guid ProjectId) : IQuery<ProjectResponse>;
