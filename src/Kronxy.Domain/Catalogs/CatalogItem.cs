using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Catalogs;

public sealed class CatalogItem : Entity
{
    private CatalogItem()
    {
    }

    public Guid Id { get; private set; }
    public Guid CatalogId { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string? Value { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? UpdatedOnUtc { get; private set; }
    public DateTime? DeletedOnUtc { get; private set; }

    public static CatalogItem Create(
        Guid catalogId,
        string code,
        string name,
        string? description,
        string? value,
        int sortOrder,
        bool isSystem,
        DateTime utcNow)
    {
        return new CatalogItem
        {
            Id = Guid.NewGuid(),
            CatalogId = catalogId,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Description = description?.Trim(),
            Value = value?.Trim(),
            SortOrder = sortOrder,
            IsSystem = isSystem,
            IsActive = true,
            CreatedOnUtc = utcNow
        };
    }

    public void Update(
        string code,
        string name,
        string? description,
        string? value,
        int sortOrder,
        DateTime utcNow)
    {
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Description = description?.Trim();
        Value = value?.Trim();
        SortOrder = sortOrder;
        UpdatedOnUtc = utcNow;
    }

    public void Activate(DateTime utcNow)
    {
        IsActive = true;
        DeletedOnUtc = null;
        UpdatedOnUtc = utcNow;
    }

    public void Deactivate(DateTime utcNow)
    {
        IsActive = false;
        DeletedOnUtc = utcNow;
        UpdatedOnUtc = utcNow;
    }
}