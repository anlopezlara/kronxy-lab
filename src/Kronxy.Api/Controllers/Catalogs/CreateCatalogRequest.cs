namespace Kronxy.Api.Controllers.Catalogs;

public sealed record CreateCatalogRequest(
    string Code,
    string Name,
    string? Description,
    bool IsSystem);