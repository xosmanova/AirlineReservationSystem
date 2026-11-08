using AirlineReservation.Application.Common.Interfaces;
using FluentValidation;

namespace AirlineReservation.Application.Features.Flights.Queries.SearchFlights;

/// <summary>
/// FluentValidation rules for flight search query.
/// Enforces business rules:
/// - Departure and arrival airports must be different.
/// - Departure date cannot be in the past (timezone-aware relative to Asia/Baku).
/// - Valid passenger count.
/// - Valid price boundaries.
/// - Positive pagination parameters.
/// </summary>
public class SearchFlightsQueryValidator : AbstractValidator<SearchFlightsQuery>
{
    public SearchFlightsQueryValidator(IBusinessTime businessTime)
    {
        ArgumentNullException.ThrowIfNull(businessTime);

        RuleFor(x => x.DepartureAirportId)
            .GreaterThan(0)
            .WithMessage("Departure airport is required.");

        RuleFor(x => x.ArrivalAirportId)
            .GreaterThan(0)
            .WithMessage("Arrival airport is required.");

        RuleFor(x => x.ArrivalAirportId)
            .NotEqual(x => x.DepartureAirportId)
            .WithMessage("Departure airport and arrival airport cannot be the same.");

        RuleFor(x => x.DepartureDate)
            .NotEmpty()
            .WithMessage("Departure date is required.")
            .Must(date => date.Date >= businessTime.TodayLocal)
            .WithMessage("Departure date cannot be in the past.");

        RuleFor(x => x.PassengersCount)
            .GreaterThan(0)
            .When(x => x.PassengersCount.HasValue)
            .WithMessage("Passenger count must be at least 1.");

        RuleFor(x => x.MinPrice)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MinPrice.HasValue)
            .WithMessage("Minimum price filter must be greater than or equal to zero.");

        RuleFor(x => x.MaxPrice)
            .GreaterThan(0)
            .When(x => x.MaxPrice.HasValue)
            .WithMessage("Maximum price filter must be greater than zero.");

        RuleFor(x => x)
            .Must(x => !x.MinPrice.HasValue || !x.MaxPrice.HasValue || x.MaxPrice >= x.MinPrice)
            .WithMessage("Maximum price cannot be less than minimum price.")
            .When(x => x.MinPrice.HasValue && x.MaxPrice.HasValue);

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100.");
    }
}
