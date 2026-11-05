using AirlineReservation.Application.DTOs.Passenger;
using FluentValidation;

namespace AirlineReservation.Application.Validators;

/// <summary>
/// FluentValidation rules for passenger details including passport, email, and phone formats.
/// </summary>
public class PassengerValidator : AbstractValidator<PassengerCreateDto>
{
    public PassengerValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(50).WithMessage("First name cannot exceed 50 characters.")
            .Matches(@"^[a-zA-Z\s\-]+$").WithMessage("First name can only contain letters, spaces, or hyphens.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(50).WithMessage("Last name cannot exceed 50 characters.")
            .Matches(@"^[a-zA-Z\s\-]+$").WithMessage("Last name can only contain letters, spaces, or hyphens.");

        RuleFor(x => x.PassportNumber)
            .NotEmpty().WithMessage("Passport number is required.")
            .Matches(@"^[A-Z0-9]{6,12}$").WithMessage("Passport number must be 6 to 12 uppercase alphanumeric characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(@"^\+?[0-9\s\-()]{7,20}$").WithMessage("A valid contact phone number is required.");

        RuleFor(x => x.DateOfBirth)
            .NotEmpty().WithMessage("Date of birth is required.")
            .Must(dob => dob.Date < DateTime.UtcNow.Date).WithMessage("Date of birth cannot be in the future.");

        RuleFor(x => x.SeatId)
            .GreaterThan(0).WithMessage("Valid seat selection is required.");
    }
}
