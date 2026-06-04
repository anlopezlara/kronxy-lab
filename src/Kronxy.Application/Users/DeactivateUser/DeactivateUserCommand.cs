using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Users.DeactivateUser;

public sealed record DeactivateUserCommand(Guid UserId) : ICommand;