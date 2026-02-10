using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Reviews.Events;

public sealed record ReviewCreatedDomainEvent(Guid ReviewId) : IDomainEvent;