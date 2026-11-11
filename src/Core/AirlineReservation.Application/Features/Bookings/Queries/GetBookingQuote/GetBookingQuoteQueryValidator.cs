using FluentValidation;

namespace AirlineReservation.Application.Features.Bookings.Queries.GetBookingQuote;

/// <summary>
/// Validates quote requests (positive flight ID and 1 to 9 distinct positive seat IDs).
/// </summary>
public sealed class GetBookingQuoteQueryValidator : AbstractValidator<GetBookingQuoteQuery>
{
    public const int MaxSeatsPerQuote = 9;

    public GetBookingQuoteQueryValidator()
    {
        RuleFor(x => x.FlightId)
            .GreaterThan(0)
            .WithMessage("FlightId must be greater than 0.");

        RuleFor(x => x.SeatIds)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage("SeatIds are required.")
            .Must(ids => ids.Count >= 1 && ids.Count <= MaxSeatsPerQuote)
            .WithMessage($"You must select between 1 and {MaxSeatsPerQuote} seats.")
            .Must(ids => ids.All(id => id > 0))
            .WithMessage("All seat IDs must be greater than 0.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Duplicate seat IDs are not allowed.");
    }
}
