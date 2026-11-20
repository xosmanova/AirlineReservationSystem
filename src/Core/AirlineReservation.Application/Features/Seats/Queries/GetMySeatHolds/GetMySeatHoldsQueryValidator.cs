using FluentValidation;

namespace AirlineReservation.Application.Features.Seats.Queries.GetMySeatHolds;

/// <summary>
/// Validator ensuring valid flight and user identity when querying personal seat holds.
/// </summary>
public class GetMySeatHoldsQueryValidator : AbstractValidator<GetMySeatHoldsQuery>
{
    public GetMySeatHoldsQueryValidator()
    {
        RuleFor(x => x.FlightId)
            .GreaterThan(0)
            .WithMessage("Flight ID must be greater than zero.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID must be provided.");
    }
}
