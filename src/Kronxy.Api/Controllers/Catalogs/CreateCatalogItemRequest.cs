namespace Kronxy.Api.Controllers.Catalogs;

public sealed record CreateCatalogItemRequest(
    string Code,
    string Name,
    string? Description,
    string? Value,
    int SortOrder,
    bool IsSystem);