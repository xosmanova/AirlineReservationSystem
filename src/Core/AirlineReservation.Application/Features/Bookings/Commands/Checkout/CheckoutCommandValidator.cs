using System.Text.RegularExpressions;
using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.DTOs.Passenger;
using AirlineReservation.Application.Validators;
using FluentValidation;

namespace AirlineReservation.Application.Features.Bookings.Commands.Checkout;

/// <summary>
/// Validator for CheckoutCommand enforcing passenger counts (1-9), distinct seats,
/// Azerbaijani unicode name compatibility, timezone-aware date of birth, contact details,
/// and PCI DSS compliant payment card validation (never echoing sensitive input).
/// </summary>
public class CheckoutCommandValidator : AbstractValidator<CheckoutCommand>
{
    private static readonly Regex PhoneRegex = new(@"^\+?[0-9\s\-()]{7,20}$", RegexOptions.Compiled);
    private static readonly Regex ExpiryRegex = new(@"^(0[1-9]|1[0-2])\/\d{2}$", RegexOptions.Compiled);

    private readonly PassengerValidator _passengerValidator;
    private readonly IBusinessTime _businessTime;

    public CheckoutCommandValidator(IBusinessTime businessTime, PassengerValidator? passengerValidator = null)
    {
        ArgumentNullException.ThrowIfNull(businessTime);
        _businessTime = businessTime;
        _passengerValidator = passengerValidator ?? new PassengerValidator(businessTime);

        RuleFor(x => x.FlightId)
            .GreaterThan(0).WithMessage("Valid Flight ID is required.");

        RuleFor(x => x.Passengers)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Passenger list is required.")
            .NotEmpty().WithMessage("At least one passenger is required.")
            .Must(p => p.Count >= 1 && p.Count <= 9)
            .WithMessage("Booking must contain between 1 and 9 passengers.")
            .Must(passengers =>
            {
                var seatIds = passengers.Select(p => p.SeatId).ToList();
                return seatIds.Distinct().Count() == seatIds.Count;
            })
            .WithMessage("Each passenger must have a unique seat. Duplicate seat selection is not allowed.");

        // Reuse PassengerValidator for every passenger to ensure identical name, passport, DOB, and seat rules
        RuleForEach(x => x.Passengers).Custom((passenger, context) =>
        {
            if (passenger == null)
            {
                context.AddFailure("Passengers", "Passenger details cannot be null.");
                return;
            }

            var passengerDto = new PassengerCreateDto
            {
                FirstName = passenger.FirstName,
                LastName = passenger.LastName,
                PassportNumber = passenger.PassportNumber,
                DateOfBirth = passenger.DateOfBirth,
                SeatId = passenger.SeatId,
                Email = !string.IsNullOrWhiteSpace(passenger.Email) ? passenger.Email : (context.InstanceToValidate.Contact?.Email ?? string.Empty),
                PhoneNumber = !string.IsNullOrWhiteSpace(passenger.PhoneNumber) ? passenger.PhoneNumber : (context.InstanceToValidate.Contact?.PhoneNumber ?? string.Empty)
            };

            var passengerResult = _passengerValidator.Validate(passengerDto);
            if (!passengerResult.IsValid)
            {
                var index = context.InstanceToValidate.Passengers?.IndexOf(passenger) ?? 0;
                foreach (var error in passengerResult.Errors)
                {
                    context.AddFailure($"Passengers[{index}].{error.PropertyName}", error.ErrorMessage);
                }
            }
        });

        RuleFor(x => x.Contact)
            .NotNull().WithMessage("Contact information is required.");

        When(x => x.Contact != null, () =>
        {
            RuleFor(x => x.Contact.Email)
                .NotEmpty().WithMessage("Contact email is required.")
                .EmailAddress().WithMessage("A valid contact email address is required.");

            RuleFor(x => x.Contact.PhoneNumber)
                .NotEmpty().WithMessage("Contact phone number is required.")
                .Must(phone => !string.IsNullOrWhiteSpace(phone) && PhoneRegex.IsMatch(phone))
                .WithMessage("A valid contact phone number is required.");
        });

        RuleFor(x => x.Card)
            .NotNull().WithMessage("Payment card details are required.");

        When(x => x.Card != null, () =>
        {
            RuleFor(x => x.Card.CardNumber)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Card number is required.")
                .Must(cn =>
                {
                    if (string.IsNullOrWhiteSpace(cn)) return false;
                    var digitsOnly = cn.Replace(" ", "").Replace("-", "");
                    return digitsOnly.Length >= 13 && digitsOnly.Length <= 19 && digitsOnly.All(char.IsDigit);
                })
                .WithMessage("Card number must contain between 13 and 19 digits.");

            RuleFor(x => x.Card.CardHolderName)
                .NotEmpty().WithMessage("Cardholder name is required.")
                .MaximumLength(100).WithMessage("Cardholder name cannot exceed 100 characters.");

            RuleFor(x => x.Card.Cvv)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("CVV is required.")
                .Must(cvv => !string.IsNullOrWhiteSpace(cvv) && (cvv.Length == 3 || cvv.Length == 4) && cvv.All(char.IsDigit))
                .WithMessage("CVV must be 3 or 4 digits.");

            RuleFor(x => x.Card.ExpiryMonthYear)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Card expiry date is required.")
                .Must(expiry => !string.IsNullOrWhiteSpace(expiry) && ExpiryRegex.IsMatch(expiry))
                .WithMessage("Card expiry date must be in MM/YY format.")
                .Must(expiry => IsExpiryValid(expiry, _businessTime))
                .WithMessage("Card has expired or expiry date is invalid.");
        });
    }

    private static bool IsExpiryValid(string expiry, IBusinessTime businessTime)
    {
        if (string.IsNullOrWhiteSpace(expiry)) return false;
        var parts = expiry.Split('/');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], out var expMonth) || !int.TryParse(parts[1], out var expYearShort))
            return false;

        if (expMonth < 1 || expMonth > 12) return false;

        var currentYear = businessTime.TodayLocal.Year;
        var currentMonth = businessTime.TodayLocal.Month;

        var expYear = 2000 + expYearShort;

        if (expYear < currentYear) return false;
        if (expYear == currentYear && expMonth < currentMonth) return false;
        if (expYear > currentYear + 25) return false;

        return true;
    }
}
