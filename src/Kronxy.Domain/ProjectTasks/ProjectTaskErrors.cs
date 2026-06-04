using Kronxy.Domain.Abstractions;
namespace Kronxy.Domain.ProjectTasks;
public static class ProjectTaskErrors
{
    public static readonly Error NotFound =
        new(
            "ProjectTask.NotFound",
            "The project task was not found.");
}
