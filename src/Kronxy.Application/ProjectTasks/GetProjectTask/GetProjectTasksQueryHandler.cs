using Dapper;
using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
namespace Kronxy.Application.ProjectTasks.GetProjectTask;
internal sealed class GetProjectTasksQueryHandler
    : IQueryHandler<GetProjectTasksQuery, IReadOnlyList<ProjectTaskResponse>>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    public GetProjectTasksQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }
    public async Task<Result<IReadOnlyList<ProjectTaskResponse>>> Handle(
        GetProjectTasksQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();
        const string sql = """
            SELECT
                pt.id AS Id,
                pt.project_id AS ProjectId,
                p.code AS ProjectCode,
                p.name AS ProjectName,
                pt.assigned_user_id AS AssignedUserId,
                u.username AS AssignedUsername,
                CONCAT(u.first_name, ' ', u.last_name) AS AssignedFullName,
                pt.title AS Title,
                pt.description AS Description,
                pt.status AS Status,
                pt.priority AS Priority,
                pt.due_date AS DueDate,
                pt.estimated_hours AS EstimatedHours,
                pt.worked_hours AS WorkedHours,
                pt.is_active AS IsActive,
                pt.created_on_utc AS CreatedOnUtc,
                pt.updated_on_utc AS UpdatedOnUtc,
                pt.deleted_on_utc AS DeletedOnUtc
            FROM project_tasks pt
            INNER JOIN projects p ON p.id = pt.project_id
            INNER JOIN users u ON u.id = pt.assigned_user_id
            WHERE pt.is_active = TRUE
            ORDER BY pt.created_on_utc DESC
            """;
        var projectTasks = await connection.QueryAsync<ProjectTaskResponse>(sql);
        return projectTasks.ToList();
    }
}
