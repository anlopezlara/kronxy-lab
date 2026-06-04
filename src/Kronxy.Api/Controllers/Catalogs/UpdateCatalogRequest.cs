namespace Kronxy.Api.Controllers.Catalogs;

public sealed record UpdateCatalogRequest(
    string Code,
    string Name,
    string? Description);