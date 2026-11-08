using AirlineReservation.Application.Common.Helpers;
using FluentValidation;
using Microsoft.Extensions.Configuration;

namespace AirlineReservation.Application.Features.Flights.Queries.SearchFlights;

/// <summary>
/// FluentValidation rules for flight search query.
/// Enforces business rules:
/// - Departure and arrival airports must be different.
/// - Departure date cannot be in the past (timezone-aware relative to Asia/Baku).
/// - Minimum passenger count.
/// </summary>
public class SearchFlightsQueryValidator : AbstractValidator<SearchFlightsQuery>
{
    public SearchFlightsQueryValidator(TimeProvider? timeProvider = null, IConfiguration? configuration = null)
    {
        var provider = timeProvider ?? TimeProvider.System;
        var timeZoneId = configuration?["Business:TimeZone"] ?? "Asia/Baku";
        var timeZone = TimeZoneHelper.ResolveTimeZone(timeZoneId);

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
            .Must(date => date.Date >= TimeZoneHelper.GetTodayInTimeZone(provider, timeZone))
            .WithMessage("Departure date cannot be in the past.");

        RuleFor(x => x.PassengersCount)
            .GreaterThan(0)
            .When(x => x.PassengersCount.HasValue)
            .WithMessage("Passenger count must be at least 1.");

        RuleFor(x => x.MaxPrice)
            .GreaterThan(0)
            .When(x => x.MaxPrice.HasValue)
            .WithMessage("Maximum price filter must be greater than zero.");
    }
}
