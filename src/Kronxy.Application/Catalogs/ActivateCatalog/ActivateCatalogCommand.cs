using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Catalogs.ActivateCatalog;

public sealed record ActivateCatalogCommand(Guid CatalogId) : ICommand;