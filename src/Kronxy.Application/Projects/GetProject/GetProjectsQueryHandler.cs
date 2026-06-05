using Dapper;
using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
namespace Kronxy.Application.Projects.GetProject;
internal sealed class GetProjectsQueryHandler
    : IQueryHandler<GetProjectsQuery, IReadOnlyList<ProjectResponse>>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    public GetProjectsQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }
    public async Task<Result<IReadOnlyList<ProjectResponse>>> Handle(
        GetProjectsQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();
        const string sql = """
            SELECT
                p.id AS Id,
                p.code AS Code,
                p.name AS Name,
                p.description AS Description,

                p.owner_id AS OwnerId,
                u.username AS OwnerUsername,
                CONCAT(u.first_name, ' ', u.last_name) AS OwnerFullName,

                p.project_type_id AS ProjectTypeId,
                project_type.code AS ProjectTypeCode,
                project_type.name AS ProjectTypeName,

                p.project_status_id AS ProjectStatusId,
                project_status.code AS ProjectStatusCode,
                project_status.name AS ProjectStatusName,

                p.project_priority_id AS ProjectPriorityId,
                project_priority.code AS ProjectPriorityCode,
                project_priority.name AS ProjectPriorityName,
                p.start_date AS StartDate,
                p.end_date AS EndDate,
                p.is_active AS IsActive,
                p.created_on_utc AS CreatedOnUtc,
                p.updated_on_utc AS UpdatedOnUtc,
                p.deleted_on_utc AS DeletedOnUtc

            FROM projects p

            INNER JOIN users u
                ON u.id = p.owner_id

            LEFT JOIN catalog_items project_type
                ON project_type.id = p.project_type_id

            LEFT JOIN catalog_items project_status
                ON project_status.id = p.project_status_id

            LEFT JOIN catalog_items project_priority
                ON project_priority.id = p.project_priority_id

            WHERE p.is_active = TRUE

            ORDER BY p.created_on_utc DESC
            """;
        var projects = await connection.QueryAsync<ProjectResponse>(sql);
        return projects.ToList();
    }
}
