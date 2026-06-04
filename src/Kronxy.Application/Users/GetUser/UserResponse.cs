namespace Kronxy.Application.Users.GetUser;

public sealed class UserResponse
{
    public Guid Id { get; init; }

    public string Username { get; init; }

    public string FirstName { get; init; }

    public string LastName { get; init; }

    public string Email { get; init; }

    public string? PhoneNumber { get; init; }

    public Guid? RoleId { get; init; }

    public string? RoleCode { get; init; }

    public string? RoleName { get; init; }

    public bool IsActive { get; init; }

    public DateTime CreatedOnUtc { get; init; }

    public DateTime? UpdatedOnUtc { get; init; }

    public DateTime? DeletedOnUtc { get; init; }
}