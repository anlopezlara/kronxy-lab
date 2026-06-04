using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.CatalogItems.ActivateCatalogItem;

public sealed record ActivateCatalogItemCommand(Guid CatalogItemId) : ICommand;