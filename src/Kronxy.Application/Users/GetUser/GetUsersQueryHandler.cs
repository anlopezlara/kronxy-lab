using Dapper;
using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Application.Users.GetUser;
using Kronxy.Domain.Abstractions;

namespace Kronxy.Application.Users.GetUsers;

internal sealed class GetUsersQueryHandler
    : IQueryHandler<GetUsersQuery, IReadOnlyList<UserResponse>>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public GetUsersQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<Result<IReadOnlyList<UserResponse>>> Handle(
        GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
            SELECT
                u.id AS Id,
                u.username AS Username,
                u.first_name AS FirstName,
                u.last_name AS LastName,
                u.email AS Email,
                u.phone_number AS PhoneNumber,
                u.role_id AS RoleId,
                role.code AS RoleCode,
                role.name AS RoleName,
                u.is_active AS IsActive,
                u.created_on_utc AS CreatedOnUtc,
                u.updated_on_utc AS UpdatedOnUtc,
                u.deleted_on_utc AS DeletedOnUtc
            FROM users u
            LEFT JOIN catalog_items role
                ON role.id = u.role_id
            WHERE u.is_active = TRUE
            ORDER BY u.created_on_utc DESC
            """;

        var users = await connection.QueryAsync<UserResponse>(sql);

        return users.ToList();
    }
}