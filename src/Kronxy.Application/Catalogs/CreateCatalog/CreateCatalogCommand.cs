using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Catalogs.CreateCatalog;

public sealed record CreateCatalogCommand(
    string Code,
    string Name,
    string? Description,
    bool IsSystem) : ICommand<Guid>;