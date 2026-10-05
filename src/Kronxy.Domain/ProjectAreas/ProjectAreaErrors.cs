using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.ProjectAreas;

public static class ProjectAreaErrors
{
    public static readonly Error NotFound =
        new(
            "ProjectArea.NotFound",
            "The project area was not found.");

    public static readonly Error WrongParent =
        new(
            "ProjectArea.WrongParent",
            "The parent project is incorrect.");

    public static readonly Error ParentNotFound =
        new(
            "ProjectArea.ParentNotFound",
            "The parent project was not found.");

    public static readonly Error ParentInactive =
        new(
            "ProjectArea.ParentInactive",
            "The parent project is inactive.");

    public static readonly Error HasActiveChildren =
        new(
            "ProjectArea.HasActiveChildren",
            "The project area has active children.");
}
