using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.ProjectStatuses.GetProjectStatuses;
public sealed record GetProjectStatusQuery(
    Guid ProjectStatusId) : IQuery<ProjectStatusResponse>;
