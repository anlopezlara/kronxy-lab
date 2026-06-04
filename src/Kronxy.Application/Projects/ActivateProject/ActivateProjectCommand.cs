using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.Projects.ActivateProject;
public sealed record ActivateProjectCommand(Guid ProjectId) : ICommand;
