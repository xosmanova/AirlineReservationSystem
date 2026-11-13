using FluentValidation;

namespace AirlineReservation.Application.Features.Seats.Commands.ReleaseSeatHolds;

/// <summary>
/// Validator ensuring valid flight and user identity when releasing personal seat holds.
/// </summary>
public class ReleaseSeatHoldsCommandValidator : AbstractValidator<ReleaseSeatHoldsCommand>
{
    public ReleaseSeatHoldsCommandValidator()
    {
        RuleFor(x => x.FlightId)
            .GreaterThan(0)
            .WithMessage("Flight ID must be greater than zero.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID must be provided.");
    }
}
