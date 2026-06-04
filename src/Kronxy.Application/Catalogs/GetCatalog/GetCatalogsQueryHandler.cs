using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Dapper;

namespace Kronxy.Application.Catalogs.GetCatalog;

internal sealed class GetCatalogsQueryHandler
    : IQueryHandler<GetCatalogsQuery, IReadOnlyList<CatalogResponse>>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public GetCatalogsQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<Result<IReadOnlyList<CatalogResponse>>> Handle(
        GetCatalogsQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
            SELECT
                id AS Id,
                code AS Code,
                name AS Name,
                description AS Description,
                is_system AS IsSystem,
                is_active AS IsActive,
                created_on_utc AS CreatedOnUtc,
                updated_on_utc AS UpdatedOnUtc,
                deleted_on_utc AS DeletedOnUtc
            FROM catalogs
            ORDER BY name
            """;

        var catalogs = await connection.QueryAsync<CatalogResponse>(sql);

        return catalogs.ToList();
    }
}