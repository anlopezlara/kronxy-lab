using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Dapper;

namespace Kronxy.Application.CatalogItems.GetCatalogItem;

internal sealed class GetCatalogItemsByCatalogCodeQueryHandler
    : IQueryHandler<GetCatalogItemsByCatalogCodeQuery, IReadOnlyList<CatalogItemResponse>>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public GetCatalogItemsByCatalogCodeQueryHandler(
        ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<Result<IReadOnlyList<CatalogItemResponse>>> Handle(
        GetCatalogItemsByCatalogCodeQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
            SELECT
                ci.id AS Id,
                ci.catalog_id AS CatalogId,
                ci.code AS Code,
                ci.name AS Name,
                ci.description AS Description,
                ci.value AS Value,
                ci.sort_order AS SortOrder,
                ci.is_system AS IsSystem,
                ci.is_active AS IsActive,
                ci.created_on_utc AS CreatedOnUtc,
                ci.updated_on_utc AS UpdatedOnUtc,
                ci.deleted_on_utc AS DeletedOnUtc
            FROM catalog_items ci
            INNER JOIN catalogs c
                ON c.id = ci.catalog_id
            WHERE c.code = @CatalogCode
            ORDER BY ci.sort_order, ci.name
            """;

        var items = await connection.QueryAsync<CatalogItemResponse>(
            sql,
            new { CatalogCode = request.CatalogCode.Trim().ToUpperInvariant() });

        return items.ToList();
    }
}