using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Users.ActivateUser;

public sealed record ActivateUserCommand(Guid UserId) : ICommand;
