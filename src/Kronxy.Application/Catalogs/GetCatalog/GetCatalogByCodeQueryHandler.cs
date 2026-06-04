using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Catalogs;
using Dapper;

namespace Kronxy.Application.Catalogs.GetCatalog;

internal sealed class GetCatalogByCodeQueryHandler
    : IQueryHandler<GetCatalogByCodeQuery, CatalogResponse>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public GetCatalogByCodeQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<Result<CatalogResponse>> Handle(
        GetCatalogByCodeQuery request,
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
            WHERE code = @Code
            """;

        CatalogResponse? catalog =
            await connection.QueryFirstOrDefaultAsync<CatalogResponse>(
                sql,
                new { Code = request.Code.Trim().ToUpperInvariant() });

        if (catalog is null)
        {
            return Result.Failure<CatalogResponse>(CatalogErrors.NotFound);
        }

        return catalog;
    }
}