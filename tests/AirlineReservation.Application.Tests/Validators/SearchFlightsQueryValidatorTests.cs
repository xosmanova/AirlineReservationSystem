using AirlineReservation.Application.Features.Flights.Queries.SearchFlights;
using Xunit;

namespace AirlineReservation.Application.Tests.Validators;

public class SearchFlightsQueryValidatorTests
{
    private readonly SearchFlightsQueryValidator _validator = new(TimeProvider.System);

    [Fact]
    public void Validator_RejectsSameDepartureAndArrivalAirport()
    {
        var query = new SearchFlightsQuery(
            DepartureAirportId: 1,
            ArrivalAirportId: 1,
            DepartureDate: DateTime.UtcNow.Date.AddDays(1)
        );

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SearchFlightsQuery.ArrivalAirportId));
    }

    [Fact]
    public void Validator_RejectsPastDepartureDate()
    {
        var query = new SearchFlightsQuery(
            DepartureAirportId: 1,
            ArrivalAirportId: 2,
            DepartureDate: DateTime.UtcNow.Date.AddDays(-2)
        );

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SearchFlightsQuery.DepartureDate));
    }

    [Fact]
    public void Validator_AcceptsValidFlightSearchQuery()
    {
        var query = new SearchFlightsQuery(
            DepartureAirportId: 1,
            ArrivalAirportId: 2,
            DepartureDate: DateTime.UtcNow.Date.AddDays(5),
            PassengersCount: 2,
            MaxPrice: 500m
        );

        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }
}
