using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.CatalogItems.GetCatalogItem;

public sealed record GetCatalogItemQuery(Guid CatalogItemId) : IQuery<CatalogItemResponse>;