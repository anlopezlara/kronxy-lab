using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Users.CreateUser;

public sealed record CreateUserCommand(
    string Username,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    Guid RoleId) : ICommand<Guid>;