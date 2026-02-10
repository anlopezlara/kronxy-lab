using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Bookings.Events;

public sealed record BookingRejectedDomainEvent(Guid BookingId) : IDomainEvent;