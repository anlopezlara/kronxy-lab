using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Catalogs.GetCatalog;

public sealed record GetCatalogsQuery() : IQuery<IReadOnlyList<CatalogResponse>>;