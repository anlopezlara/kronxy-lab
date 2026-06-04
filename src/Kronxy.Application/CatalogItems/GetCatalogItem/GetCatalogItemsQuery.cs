using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.CatalogItems.GetCatalogItem;

public sealed record GetCatalogItemsQuery(Guid CatalogId) : IQuery<IReadOnlyList<CatalogItemResponse>>;