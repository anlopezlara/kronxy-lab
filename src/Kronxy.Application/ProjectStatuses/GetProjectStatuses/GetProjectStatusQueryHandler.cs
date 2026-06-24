using Dapper;
using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Projects;
namespace Kronxy.Application.ProjectStatuses.GetProjectStatuses;
internal sealed class GetProjectStatusQueryHandler
    : IQueryHandler<GetProjectStatusQuery, ProjectStatusResponse>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    public GetProjectStatusQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }
    public async Task<Result<ProjectStatusResponse>> Handle(
        GetProjectStatusQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();
        const string sql = """
            SELECT
                id AS Id,
                code AS Code,
                name AS Name,
                description AS Description,
                display_order AS DisplayOrder,
                is_active AS IsActive,
                created_on_utc AS CreatedOnUtc,
                updated_on_utc AS UpdatedOnUtc,
                deleted_on_utc AS DeletedOnUtc
            FROM project_statuses
            WHERE id = @ProjectStatusId
            """;
        ProjectStatusResponse? projectStatus =
            await connection.QueryFirstOrDefaultAsync<ProjectStatusResponse>(
                sql,
                new { request.ProjectStatusId });
        if (projectStatus is null)
        {
            return Result.Failure<ProjectStatusResponse>(ProjectErrors.InvalidProjectStatus);
        }
        return projectStatus;
    }
}
