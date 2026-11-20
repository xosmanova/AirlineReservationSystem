using System.Text.RegularExpressions;
using FluentValidation;

namespace AirlineReservation.Application.Features.Bookings.Queries.GetBookingByPnr;

/// <summary>
/// Validator for PNR lookup query enforcing 6-character alphanumeric PNR and non-empty passenger last name.
/// </summary>
public class GetBookingByPnrQueryValidator : AbstractValidator<GetBookingByPnrQuery>
{
    private static readonly Regex PnrRegex = new(@"^[A-Z0-9]{6}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public GetBookingByPnrQueryValidator()
    {
        RuleFor(x => x.PnrCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("PNR code is required.")
            .Length(6).WithMessage("PNR code must be exactly 6 characters.")
            .Must(p => !string.IsNullOrWhiteSpace(p) && PnrRegex.IsMatch(p.Trim()))
            .WithMessage("PNR code must be 6 alphanumeric characters.");

        RuleFor(x => x.LastName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Passenger last name is required.")
            .MaximumLength(50).WithMessage("Passenger last name cannot exceed 50 characters.");
    }
}
