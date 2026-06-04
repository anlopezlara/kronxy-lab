using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Projects;

public static class ProjectErrors
{
    public static readonly Error NotFound =
        new(
            "Project.NotFound",
            "The project was not found.");

    public static readonly Error CodeAlreadyInUse =
        new(
            "Project.CodeAlreadyInUse",
            "The project code is already in use.");

    public static readonly Error InvalidProjectType = new(
            "Project.InvalidProjectType",
            "The selected project type is invalid.");

    public static readonly Error InvalidProjectStatus = new(
            "Project.InvalidProjectStatus",
            "The selected project status is invalid.");

}