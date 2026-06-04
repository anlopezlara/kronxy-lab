using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Catalogs;
using Dapper;

namespace Kronxy.Application.CatalogItems.GetCatalogItem;

internal sealed class GetCatalogItemQueryHandler
    : IQueryHandler<GetCatalogItemQuery, CatalogItemResponse>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public GetCatalogItemQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<Result<CatalogItemResponse>> Handle(
        GetCatalogItemQuery request,
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
            WHERE id = @CatalogItemId
            """;

        CatalogItemResponse? item =
            await connection.QueryFirstOrDefaultAsync<CatalogItemResponse>(
                sql,
                new { request.CatalogItemId });

        if (item is null)
        {
            return Result.Failure<CatalogItemResponse>(CatalogErrors.ItemNotFound);
        }

        return item;
    }
}