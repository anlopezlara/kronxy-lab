using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Catalogs.GetCatalog;

public sealed record GetCatalogByCodeQuery(string Code) : IQuery<CatalogResponse>;