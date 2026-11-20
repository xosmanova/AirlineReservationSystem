using FluentValidation;

namespace AirlineReservation.Application.Features.Bookings.Commands.CancelBooking;

/// <summary>
/// Validator for CancelBookingCommand.
/// </summary>
public class CancelBookingCommandValidator : AbstractValidator<CancelBookingCommand>
{
    public CancelBookingCommandValidator()
    {
        RuleFor(x => x.BookingId)
            .GreaterThan(0)
            .WithMessage("Booking ID must be greater than 0.");
    }
}
