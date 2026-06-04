using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Users.UpdateUser;

public sealed record UpdateUserCommand(
    Guid UserId,
    string Username,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    Guid RoleId) : ICommand;