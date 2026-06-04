using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.CatalogItems.UpdateCatalogItem;

public sealed record UpdateCatalogItemCommand(
    Guid CatalogItemId,
    string Code,
    string Name,
    string? Description,
    string? Value,
    int SortOrder) : ICommand;