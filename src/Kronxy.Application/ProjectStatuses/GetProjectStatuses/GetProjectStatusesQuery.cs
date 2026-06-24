using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.ProjectStatuses.GetProjectStatuses;
public sealed record GetProjectStatusesQuery(
    bool ActiveOnly = false) : IQuery<IReadOnlyList<ProjectStatusResponse>>;
