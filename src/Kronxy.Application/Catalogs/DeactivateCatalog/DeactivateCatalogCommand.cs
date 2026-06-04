using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Catalogs.DeactivateCatalog;

public sealed record DeactivateCatalogCommand(Guid CatalogId) : ICommand;