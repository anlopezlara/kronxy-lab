using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Catalogs.UpdateCatalog;

public sealed record UpdateCatalogCommand(
    Guid CatalogId,
    string Code,
    string Name,
    string? Description) : ICommand;