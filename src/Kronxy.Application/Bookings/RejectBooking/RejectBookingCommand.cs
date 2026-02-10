using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Bookings.RejectBooking;

public sealed record RejectBookingCommand(Guid BookingId) : ICommand;