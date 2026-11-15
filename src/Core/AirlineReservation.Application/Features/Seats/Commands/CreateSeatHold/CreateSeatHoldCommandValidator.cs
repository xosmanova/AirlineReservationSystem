using FluentValidation;

namespace AirlineReservation.Application.Features.Seats.Commands.CreateSeatHold;

/// <summary>
/// Validator ensuring seat hold request contains 1 to 9 distinct valid positive seat IDs for a valid flight and user.
/// </summary>
public class CreateSeatHoldCommandValidator : AbstractValidator<CreateSeatHoldCommand>
{
    public CreateSeatHoldCommandValidator()
    {
        RuleFor(x => x.FlightId)
            .GreaterThan(0)
            .WithMessage("Flight ID must be greater than zero.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID must be provided.");

        RuleFor(x => x.SeatIds)
            .NotNull()
            .WithMessage("At least one seat must be selected.")
            .NotEmpty()
            .WithMessage("At least one seat must be selected.")
            .Must(s => s == null || (s.Count >= 1 && s.Count <= 9))
            .WithMessage("You can hold between 1 and 9 seats at a time.")
            .Must(s => s == null || s.Distinct().Count() == s.Count)
            .WithMessage("Duplicate seat selections are not allowed.");

        RuleForEach(x => x.SeatIds)
            .GreaterThan(0)
            .WithMessage("Seat ID must be greater than zero.");
    }
}
