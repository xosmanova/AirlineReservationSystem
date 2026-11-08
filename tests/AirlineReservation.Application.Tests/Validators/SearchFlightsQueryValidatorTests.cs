using AirlineReservation.Application.Common.Helpers;
using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.Common.Services;
using AirlineReservation.Application.Features.Flights.Queries.SearchFlights;
using Xunit;

namespace AirlineReservation.Application.Tests.Validators;

public class SearchFlightsQueryValidatorTests
{
    private static readonly TimeZoneInfo BakuTimeZone = TimeZoneHelper.ResolveTimeZone("Asia/Baku");
    private readonly IBusinessTime _businessTime = new BusinessTimeService(TimeProvider.System, BakuTimeZone);
    private readonly SearchFlightsQueryValidator _validator;

    public SearchFlightsQueryValidatorTests()
    {
        _validator = new SearchFlightsQueryValidator(_businessTime);
    }

    [Fact]
    public void Validator_RejectsSameDepartureAndArrivalAirport()
    {
        var query = new SearchFlightsQuery(
            DepartureAirportId: 1,
            ArrivalAirportId: 1,
            DepartureDate: _businessTime.TodayLocal.AddDays(1)
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
            DepartureDate: _businessTime.TodayLocal.AddDays(-2)
        );

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SearchFlightsQuery.DepartureDate));
    }

    [Fact]
    public void Validator_RejectsWhenMaxPriceLessThanMinPrice()
    {
        var query = new SearchFlightsQuery(
            DepartureAirportId: 1,
            ArrivalAirportId: 2,
            DepartureDate: _businessTime.TodayLocal.AddDays(5),
            MinPrice: 300m,
            MaxPrice: 100m
        );

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_AcceptsValidFlightSearchQuery()
    {
        var query = new SearchFlightsQuery(
            DepartureAirportId: 1,
            ArrivalAirportId: 2,
            DepartureDate: _businessTime.TodayLocal.AddDays(5),
            PassengersCount: 2,
            MinPrice: 50m,
            MaxPrice: 500m
        );

        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }
}
