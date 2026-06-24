using Dapper;
using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
namespace Kronxy.Application.ProjectStatuses.GetProjectStatuses;
internal sealed class GetProjectStatusesQueryHandler
    : IQueryHandler<GetProjectStatusesQuery, IReadOnlyList<ProjectStatusResponse>>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    public GetProjectStatusesQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }
    public async Task<Result<IReadOnlyList<ProjectStatusResponse>>> Handle(
        GetProjectStatusesQuery request,
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
            WHERE (@ActiveOnly = FALSE OR is_active = TRUE)
            ORDER BY display_order ASC, name ASC
            """;
        var projectStatuses =
            await connection.QueryAsync<ProjectStatusResponse>(
                sql,
                new { request.ActiveOnly });
        return projectStatuses.ToList();
    }
}
