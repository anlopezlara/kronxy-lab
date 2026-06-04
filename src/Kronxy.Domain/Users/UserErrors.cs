using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Users;

public static class UserErrors
{
    public static Error NotFound = new(
        "User.Found",
        "The user with the specified identifier was not found");

    public static Error InvalidCredentials = new(
        "User.InvalidCredentials",
        "The provided credentials were invalid");

    public static Error EmailAlreadyExists = new(
    "User.EmailAlreadyExists",
    "The specified email already exists");

    public static Error UsernameAlreadyExists = new(
        "User.UsernameAlreadyExists",
        "The specified username already exists");

    public static Error InvalidRole = new(
    "User.InvalidRole",
    "The specified role is not valid");

}