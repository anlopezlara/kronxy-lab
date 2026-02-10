using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Bookings.CompleteBooking;

public record CompleteBookingCommand(Guid BookingId) : ICommand;