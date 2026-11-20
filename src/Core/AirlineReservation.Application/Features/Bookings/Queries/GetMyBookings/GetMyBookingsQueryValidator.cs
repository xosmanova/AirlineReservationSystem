using FluentValidation;

namespace AirlineReservation.Application.Features.Bookings.Queries.GetMyBookings;

/// <summary>
/// Validator for GetMyBookingsQuery enforcing positive pagination and allowable status filter values.
/// </summary>
public class GetMyBookingsQueryValidator : AbstractValidator<GetMyBookingsQuery>
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "upcoming",
        "past",
        "cancelled"
    };

    public GetMyBookingsQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User identity is required.");

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Page size must be between 1 and 50.");

        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrWhiteSpace(s) || AllowedStatuses.Contains(s.Trim()))
            .WithMessage("Status filter must be one of: 'upcoming', 'past', or 'cancelled'.");
    }
}
