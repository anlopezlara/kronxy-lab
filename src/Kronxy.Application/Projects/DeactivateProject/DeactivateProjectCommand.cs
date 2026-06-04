using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.Projects.DeactivateProject;
public sealed record DeactivateProjectCommand(Guid ProjectId) : ICommand;
