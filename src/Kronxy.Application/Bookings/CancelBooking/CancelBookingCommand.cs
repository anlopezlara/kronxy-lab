using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Bookings.CancelBooking;

public record CancelBookingCommand(Guid BookingId) : ICommand;