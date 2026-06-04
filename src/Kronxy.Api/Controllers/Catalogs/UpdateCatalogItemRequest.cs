namespace Kronxy.Api.Controllers.Catalogs;

public sealed record UpdateCatalogItemRequest(
    string Code,
    string Name,
    string? Description,
    string? Value,
    int SortOrder);