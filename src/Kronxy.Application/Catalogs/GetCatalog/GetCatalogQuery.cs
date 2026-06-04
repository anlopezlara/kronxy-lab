using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Catalogs.GetCatalog;

public sealed record GetCatalogQuery(Guid CatalogId) : IQuery<CatalogResponse>;