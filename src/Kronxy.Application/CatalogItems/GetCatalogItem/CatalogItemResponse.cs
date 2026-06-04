namespace Kronxy.Application.CatalogItems.GetCatalogItem;

public sealed class CatalogItemResponse
{
    public Guid Id { get; init; }

    public Guid CatalogId { get; init; }

    public string Code { get; init; }

    public string Name { get; init; }

    public string? Description { get; init; }

    public string? Value { get; init; }

    public int SortOrder { get; init; }

    public bool IsSystem { get; init; }

    public bool IsActive { get; init; }

    public DateTime CreatedOnUtc { get; init; }

    public DateTime? UpdatedOnUtc { get; init; }

    public DateTime? DeletedOnUtc { get; init; }
}