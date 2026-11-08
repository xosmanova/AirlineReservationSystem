using AirlineReservation.Application.Common.Helpers;
using AirlineReservation.Application.DTOs.Passenger;
using FluentValidation;
using Microsoft.Extensions.Configuration;

namespace AirlineReservation.Application.Validators;

/// <summary>
/// FluentValidation rules for passenger details.
/// Supports international and Azerbaijani unicode names (Ə, ö, ü, ç, ş, ğ, ı),
/// case-insensitive passport numbers, and timezone-aware DateOfBirth validation.
/// </summary>
public class PassengerValidator : AbstractValidator<PassengerCreateDto>
{
    private const string NameRegex = @"^[\p{L}\s\-']+$";
    private const string PassportRegex = @"^[a-zA-Z0-9]{6,12}$";
    private const string PhoneRegex = @"^\+?[0-9\s\-()]{7,20}$";

    public PassengerValidator(TimeProvider? timeProvider = null, IConfiguration? configuration = null)
    {
        var provider = timeProvider ?? TimeProvider.System;
        var timeZoneId = configuration?["Business:TimeZone"] ?? "Asia/Baku";
        var timeZone = TimeZoneHelper.ResolveTimeZone(timeZoneId);

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(50).WithMessage("First name cannot exceed 50 characters.")
            .Matches(NameRegex).WithMessage("First name can only contain letters, spaces, hyphens, or apostrophes.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(50).WithMessage("Last name cannot exceed 50 characters.")
            .Matches(NameRegex).WithMessage("Last name can only contain letters, spaces, hyphens, or apostrophes.");

        RuleFor(x => x.PassportNumber)
            .NotEmpty().WithMessage("Passport number is required.")
            .Matches(PassportRegex).WithMessage("Passport number must be 6 to 12 alphanumeric characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(PhoneRegex).WithMessage("A valid contact phone number is required.");

        RuleFor(x => x.DateOfBirth)
            .NotEmpty().WithMessage("Date of birth is required.")
            .Must(dob =>
            {
                var today = TimeZoneHelper.GetTodayInTimeZone(provider, timeZone);
                var dobDate = dob.Date;
                return dobDate <= today && dobDate >= today.AddYears(-120);
            })
            .WithMessage("Date of birth cannot be in the future and passenger cannot be older than 120 years.");

        RuleFor(x => x.SeatId)
            .GreaterThan(0).WithMessage("Valid seat selection is required.");
    }
}
