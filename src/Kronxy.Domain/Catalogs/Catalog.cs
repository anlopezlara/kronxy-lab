using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Catalogs;

public sealed class Catalog : Entity
{
    private readonly List<CatalogItem> _items = new();

    private Catalog()
    {
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? UpdatedOnUtc { get; private set; }
    public DateTime? DeletedOnUtc { get; private set; }

    public IReadOnlyCollection<CatalogItem> Items => _items;

    public static Catalog Create(
        string code,
        string name,
        string? description,
        bool isSystem,
        DateTime utcNow)
    {
        return new Catalog
        {
            Id = Guid.NewGuid(),
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Description = description?.Trim(),
            IsSystem = isSystem,
            IsActive = true,
            CreatedOnUtc = utcNow
        };
    }

    public void Update(
        string code,
        string name,
        string? description,
        DateTime utcNow)
    {
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Description = description?.Trim();
        UpdatedOnUtc = utcNow;
    }

    public CatalogItem AddItem(
        string code,
        string name,
        string? description,
        string? value,
        int sortOrder,
        bool isSystem,
        DateTime utcNow)
    {
        var item = CatalogItem.Create(
            Id,
            code,
            name,
            description,
            value,
            sortOrder,
            isSystem,
            utcNow);

        _items.Add(item);

        UpdatedOnUtc = utcNow;

        return item;
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