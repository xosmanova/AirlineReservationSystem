using FluentValidation;

namespace AirlineReservation.Application.Features.Bookings.Queries.CancelBookingPreview;

/// <summary>
/// Validator for CancelBookingPreviewQuery.
/// </summary>
public class CancelBookingPreviewQueryValidator : AbstractValidator<CancelBookingPreviewQuery>
{
    public CancelBookingPreviewQueryValidator()
    {
        RuleFor(x => x.BookingId)
            .GreaterThan(0)
            .WithMessage("Booking ID must be greater than 0.");
    }
}
