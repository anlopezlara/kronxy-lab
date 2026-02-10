using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Bookings.GetBooking;

public sealed record GetBookingQuery(Guid BookingId) : IQuery<BookingResponse>;