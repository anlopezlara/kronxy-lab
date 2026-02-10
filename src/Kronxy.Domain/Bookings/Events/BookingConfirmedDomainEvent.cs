using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Bookings.Events;

public sealed record BookingConfirmedDomainEvent(Guid BookingId) : IDomainEvent;