using Dapper;
using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Users;

namespace Kronxy.Application.Users.GetUser;

internal sealed class GetUserQueryHandler : IQueryHandler<GetUserQuery, UserResponse>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public GetUserQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<Result<UserResponse>> Handle(
        GetUserQuery request,
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
            WHERE u.id = @UserId
            """;

        var user = await connection.QueryFirstOrDefaultAsync<UserResponse>(
            sql,
            new
            {
                request.UserId
            });

        if (user is null)
        {
            return Result.Failure<UserResponse>(UserErrors.NotFound);
        }

        return user;
    }
}