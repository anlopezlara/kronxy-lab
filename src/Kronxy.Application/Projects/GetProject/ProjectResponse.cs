namespace Kronxy.Application.Projects.GetProject;

public sealed class ProjectResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public Guid OwnerId { get; init; }

    public string OwnerUsername { get; init; } = string.Empty;

    public string OwnerFullName { get; init; } = string.Empty;

    public Guid ProjectTypeId { get; init; }

    public string ProjectTypeCode { get; init; } = string.Empty;

    public string ProjectTypeName { get; init; } = string.Empty;

    public Guid ProjectStatusId { get; init; }

    public string ProjectStatusCode { get; init; } = string.Empty;

    public string ProjectStatusName { get; init; } = string.Empty;

    public int Priority { get; init; }

    public DateOnly? StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    public bool IsActive { get; init; }

    public DateTime CreatedOnUtc { get; init; }

    public DateTime? UpdatedOnUtc { get; init; }

    public DateTime? DeletedOnUtc { get; init; }
}