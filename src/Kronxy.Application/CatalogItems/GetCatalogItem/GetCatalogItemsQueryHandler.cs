using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Dapper;

namespace Kronxy.Application.CatalogItems.GetCatalogItem;

internal sealed class GetCatalogItemsQueryHandler
    : IQueryHandler<GetCatalogItemsQuery, IReadOnlyList<CatalogItemResponse>>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public GetCatalogItemsQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<Result<IReadOnlyList<CatalogItemResponse>>> Handle(
        GetCatalogItemsQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
            SELECT
                id AS Id,
                catalog_id AS CatalogId,
                code AS Code,
                name AS Name,
                description AS Description,
                value AS Value,
                sort_order AS SortOrder,
                is_system AS IsSystem,
                is_active AS IsActive,
                created_on_utc AS CreatedOnUtc,
                updated_on_utc AS UpdatedOnUtc,
                deleted_on_utc AS DeletedOnUtc
            FROM catalog_items
            WHERE catalog_id = @CatalogId
            ORDER BY sort_order, name
            """;

        var items = await connection.QueryAsync<CatalogItemResponse>(
            sql,
            new { request.CatalogId });

        return items.ToList();
    }
}