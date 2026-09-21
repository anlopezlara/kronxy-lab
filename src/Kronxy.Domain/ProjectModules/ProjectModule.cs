namespace Kronxy.Domain.ProjectModules;

using Kronxy.Domain.Abstractions;

public sealed class ProjectModule : Entity
{
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? UpdatedOnUtc { get; private set; }
    public DateTime? DeletedOnUtc { get; private set; }

    public static ProjectModule Create(
        Guid projectId,
        string name,
        string? description,
        DateTime utcNow)
    {
        var projectModule = new ProjectModule
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name,
            Description = description,
            IsActive = true,
            CreatedOnUtc = utcNow
        };

        return projectModule;
    }

    public void Update(
        string name,
        string? description,
        DateTime utcNow)
    {
        Name = name;
        Description = description;
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
