using Kronxy.Domain.Abstractions;
namespace Kronxy.Domain.Users;
public sealed class UserRole : Entity
{
    private UserRole() { }
    private UserRole(Guid id) : base(id) { }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? UpdatedOnUtc { get; private set; }
    public DateTime? DeletedOnUtc { get; private set; }
    public static UserRole Create(string code, string name, string? description, int displayOrder, DateTime utcNow)
        => new(Guid.NewGuid())
        {
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Description = description?.Trim(),
            DisplayOrder = displayOrder,
            IsActive = true,
            CreatedOnUtc = utcNow
        };
    public void Update(string code, string name, string? description, int displayOrder, DateTime utcNow)
    {
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Description = description?.Trim();
        DisplayOrder = displayOrder;
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
