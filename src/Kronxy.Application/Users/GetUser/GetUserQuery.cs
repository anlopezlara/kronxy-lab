using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Users.GetUser;

public sealed record GetUserQuery(Guid UserId) : IQuery<UserResponse>;