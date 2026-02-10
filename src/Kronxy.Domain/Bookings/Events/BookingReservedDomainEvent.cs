using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Bookings.Events;

public sealed record BookingReservedDomainEvent(Guid BookingId) : IDomainEvent;