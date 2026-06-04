using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.CatalogItems.DeactivateCatalogItem;

public sealed record DeactivateCatalogItemCommand(Guid CatalogItemId) : ICommand;