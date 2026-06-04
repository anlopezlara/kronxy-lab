using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.CatalogItems.CreateCatalogItem;

public sealed record CreateCatalogItemCommand(
    Guid CatalogId,
    string Code,
    string Name,
    string? Description,
    string? Value,
    int SortOrder,
    bool IsSystem) : ICommand<Guid>;