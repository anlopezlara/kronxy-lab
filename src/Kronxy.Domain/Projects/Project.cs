using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Projects;

public sealed class Project : Entity
{
    private Project()
    {
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public Guid OwnerId { get; private set; }

    public ProjectStatus Status { get; private set; }

    public ProjectPriority Priority { get; private set; }

    public DateOnly? StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime? UpdatedOnUtc { get; private set; }

    public DateTime? DeletedOnUtc { get; private set; }

    public Guid ProjectTypeId { get; private set; }

    public static Project Create(
        string code,
        string name,
        string? description,
        Guid ownerId,
        Guid projectTypeId,
        DateTime utcNow)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Description = description,
            OwnerId = ownerId,
            ProjectTypeId = projectTypeId,
            Status = ProjectStatus.Draft,
            Priority = ProjectPriority.Medium,
            IsActive = true,
            CreatedOnUtc = utcNow
        };

        return project;
    }

    public void Update(
        string code,
        string name,
        string? description,
        Guid ownerId,
        Guid projectTypeId,
        DateTime utcNow)
    {
        Code = code;
        Name = name;
        Description = description;
        OwnerId = ownerId;
        ProjectTypeId = projectTypeId;
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

    public void ChangeStatus(
        ProjectStatus status,
        DateTime utcNow)
    {
        Status = status;
        UpdatedOnUtc = utcNow;
    }

    public void ChangePriority(
        ProjectPriority priority,
        DateTime utcNow)
    {
        Priority = priority;
        UpdatedOnUtc = utcNow;
    }
}