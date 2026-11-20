using FluentValidation;

namespace AirlineReservation.Application.Features.Seats.Queries.GetFlightSeatStatus;

/// <summary>
/// Validator ensuring valid flight identifier when querying seat status updates.
/// </summary>
public class GetFlightSeatStatusQueryValidator : AbstractValidator<GetFlightSeatStatusQuery>
{
    public GetFlightSeatStatusQueryValidator()
    {
        RuleFor(x => x.FlightId)
            .GreaterThan(0)
            .WithMessage("Flight ID must be greater than zero.");
    }
}
