using FluentValidation;

namespace AirlineReservation.Application.Features.Flights.Queries.GetFlightSeatMap;

public class GetFlightSeatMapQueryValidator : AbstractValidator<GetFlightSeatMapQuery>
{
    public GetFlightSeatMapQueryValidator()
    {
        RuleFor(x => x.FlightId)
            .GreaterThan(0)
            .WithMessage("Valid flight ID is required.");
    }
}
