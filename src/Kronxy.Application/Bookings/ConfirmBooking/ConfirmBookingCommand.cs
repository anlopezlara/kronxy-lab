using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Bookings.ConfirmBooking;

public sealed record ConfirmBookingCommand(Guid BookingId) : ICommand;