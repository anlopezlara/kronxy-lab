using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Bookings.Events;

public sealed record BookingCancelledDomainEvent(Guid BookingId) : IDomainEvent;