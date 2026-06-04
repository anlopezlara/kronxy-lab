using Kronxy.Domain.Abstractions;
namespace Kronxy.Domain.ProjectTasks;
public sealed class ProjectTask : Entity
{
    private ProjectTask()
    {
    }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid AssignedUserId { get; private set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public ProjectTaskStatus Status { get; private set; }
    public ProjectTaskPriority Priority { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public decimal? EstimatedHours { get; private set; }
    public decimal? WorkedHours { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? UpdatedOnUtc { get; private set; }
    public DateTime? DeletedOnUtc { get; private set; }
    public static ProjectTask Create(
        Guid projectId,
        Guid assignedUserId,
        string title,
        string? description,
        DateTime utcNow)
    {
        return new ProjectTask
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            AssignedUserId = assignedUserId,
            Title = title,
            Description = description,
            Status = ProjectTaskStatus.Pending,
            Priority = ProjectTaskPriority.Medium,
            IsActive = true,
            CreatedOnUtc = utcNow
        };
    }
    public void Update(
        Guid assignedUserId,
        string title,
        string? description,
        DateTime utcNow)
    {
        AssignedUserId = assignedUserId;
        Title = title;
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
    public void ChangeStatus(
        ProjectTaskStatus status,
        DateTime utcNow)
    {
        Status = status;
        UpdatedOnUtc = utcNow;
    }
    public void ChangePriority(
        ProjectTaskPriority priority,
        DateTime utcNow)
    {
        Priority = priority;
        UpdatedOnUtc = utcNow;
    }
}
