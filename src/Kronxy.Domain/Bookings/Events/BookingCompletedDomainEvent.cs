using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Bookings.Events;

public sealed record BookingCompletedDomainEvent(Guid BookingId) : IDomainEvent;