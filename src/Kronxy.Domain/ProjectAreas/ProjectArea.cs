namespace Kronxy.Domain.ProjectAreas;

using Kronxy.Domain.Abstractions;

public sealed class ProjectArea : Entity
{
    public Guid Id { get; private set; }
    public Guid ProjectModuleId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? UpdatedOnUtc { get; private set; }
    public DateTime? DeletedOnUtc { get; private set; }

    public static ProjectArea Create(Guid projectModuleId, string name, string? description, DateTime createdOnUtc)
    {
        var projectArea = new ProjectArea
        {
            Id = Guid.NewGuid(),
            ProjectModuleId = projectModuleId,
            Name = name,
            Description = description,
            IsActive = true,
            CreatedOnUtc = createdOnUtc
        };

        return projectArea;
    }

    public void Update(string name, string? description, DateTime updatedOnUtc)
    {
        Name = name;
        Description = description;
        UpdatedOnUtc = updatedOnUtc;
    }

    public void Activate(DateTime updatedOnUtc)
    {
        IsActive = true;
        DeletedOnUtc = null;
        UpdatedOnUtc = updatedOnUtc;
    }

    public void Deactivate(DateTime deletedOnUtc)
    {
        IsActive = false;
        DeletedOnUtc = deletedOnUtc;
        UpdatedOnUtc = deletedOnUtc;
    }
}
