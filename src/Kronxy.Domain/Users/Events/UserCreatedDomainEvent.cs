using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Users.Events;

public sealed record UserCreatedDomainEvent(Guid UserId) : IDomainEvent;