using System.Text.RegularExpressions;
using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.DTOs.Passenger;
using FluentValidation;

namespace AirlineReservation.Application.Validators;

/// <summary>
/// FluentValidation rules for passenger details.
/// Supports international and Azerbaijani unicode names (Ə, ö, ü, ç, ş, ğ, ı),
/// whitespace-tolerant and case-insensitive passport numbers, and timezone-aware DateOfBirth validation.
/// </summary>
public class PassengerValidator : AbstractValidator<PassengerCreateDto>
{
    private static readonly Regex NameRegex = new(@"^[\p{L}\s\-']+$", RegexOptions.Compiled);
    private static readonly Regex PassportRegex = new(@"^[a-zA-Z0-9]{6,12}$", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"^\+?[0-9\s\-()]{7,20}$", RegexOptions.Compiled);

    public PassengerValidator(IBusinessTime businessTime)
    {
        ArgumentNullException.ThrowIfNull(businessTime);

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(50).WithMessage("First name cannot exceed 50 characters.")
            .Must(name => !string.IsNullOrWhiteSpace(name) && NameRegex.IsMatch(name))
            .WithMessage("First name can only contain letters, spaces, hyphens, or apostrophes.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(50).WithMessage("Last name cannot exceed 50 characters.")
            .Must(name => !string.IsNullOrWhiteSpace(name) && NameRegex.IsMatch(name))
            .WithMessage("Last name can only contain letters, spaces, hyphens, or apostrophes.");

        RuleFor(x => x.PassportNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Passport number is required.")
            .Must(p => !string.IsNullOrWhiteSpace(p) && PassportRegex.IsMatch(p.Trim()))
            .WithMessage("Passport number must be 6 to 12 alphanumeric characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .Must(phone => !string.IsNullOrWhiteSpace(phone) && PhoneRegex.IsMatch(phone))
            .WithMessage("A valid contact phone number is required.");

        RuleFor(x => x.DateOfBirth)
            .NotEmpty().WithMessage("Date of birth is required.")
            .Must(dob =>
            {
                var today = businessTime.TodayLocal;
                var dobDate = dob.Date;
                return dobDate <= today && dobDate >= today.AddYears(-120);
            })
            .WithMessage("Date of birth cannot be in the future and passenger cannot be older than 120 years.");

        RuleFor(x => x.SeatId)
            .GreaterThan(0).WithMessage("Valid seat selection is required.");
    }
}
