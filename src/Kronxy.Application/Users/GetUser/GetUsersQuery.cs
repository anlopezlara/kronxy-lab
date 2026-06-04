using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Application.Users.GetUser;

namespace Kronxy.Application.Users.GetUsers;

public sealed record GetUsersQuery
    : IQuery<IReadOnlyList<UserResponse>>;