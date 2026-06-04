namespace Kronxy.Application.Catalogs.GetCatalog;

public sealed class CatalogResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; }

    public string Name { get; init; }

    public string? Description { get; init; }

    public bool IsSystem { get; init; }

    public bool IsActive { get; init; }

    public DateTime CreatedOnUtc { get; init; }

    public DateTime? UpdatedOnUtc { get; init; }

    public DateTime? DeletedOnUtc { get; init; }
}