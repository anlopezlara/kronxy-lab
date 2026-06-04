namespace Kronxy.Application.ProjectTasks.GetProjectTask;
public sealed class ProjectTaskResponse
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public string ProjectCode { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public Guid AssignedUserId { get; init; }
    public string AssignedUsername { get; init; } = string.Empty;
    public string AssignedFullName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int Status { get; init; }
    public int Priority { get; init; }
    public DateOnly? DueDate { get; init; }
    public decimal? EstimatedHours { get; init; }
    public decimal? WorkedHours { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedOnUtc { get; init; }
    public DateTime? UpdatedOnUtc { get; init; }
    public DateTime? DeletedOnUtc { get; init; }
}
