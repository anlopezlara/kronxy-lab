namespace Kronxy.Api.Controllers.Users;

public sealed record CreateUserRequest(
    string Username,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    Guid RoleId);