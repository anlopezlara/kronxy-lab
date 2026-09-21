using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.ProjectModules;

public static class ProjectModuleErrors
{
    public static readonly Error NotFound =
        new(
            "ProjectModule.NotFound",
            "The project module was not found.");

    public static readonly Error WrongParent =
        new(
            "ProjectModule.WrongParent",
            "The parent project is incorrect.");

    public static readonly Error ParentNotFound =
        new(
            "ProjectModule.ParentNotFound",
            "The parent project was not found.");

    public static readonly Error ParentInactive =
        new(
            "ProjectModule.ParentInactive",
            "The parent project is inactive.");

    public static readonly Error HasActiveChildren =
        new(
            "ProjectModule.HasActiveChildren",
            "The project module has active children.");
}
