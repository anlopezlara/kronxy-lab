using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.CatalogItems.GetCatalogItem;

public sealed record GetCatalogItemsByCatalogCodeQuery(
    string CatalogCode) : IQuery<IReadOnlyList<CatalogItemResponse>>;