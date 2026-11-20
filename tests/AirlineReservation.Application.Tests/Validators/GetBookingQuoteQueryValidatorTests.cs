using AirlineReservation.Application.Features.Bookings.Queries.GetBookingQuote;

namespace AirlineReservation.Application.Tests.Validators;

public class GetBookingQuoteQueryValidatorTests
{
    private readonly GetBookingQuoteQueryValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validator_RejectsNonPositiveFlightId(int flightId)
    {
        var query = new GetBookingQuoteQuery(flightId, [1]);

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(GetBookingQuoteQuery.FlightId));
    }

    [Fact]
    public void Validator_RejectsEmptySeatList()
    {
        var query = new GetBookingQuoteQuery(1, []);

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(GetBookingQuoteQuery.SeatIds));
    }

    [Fact]
    public void Validator_RejectsMoreThanNineSeats()
    {
        var tenSeats = Enumerable.Range(1, 10).ToList();
        var query = new GetBookingQuoteQuery(1, tenSeats);

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(GetBookingQuoteQuery.SeatIds));
    }

    [Fact]
    public void Validator_RejectsNonPositiveSeatIds()
    {
        var query = new GetBookingQuoteQuery(1, [5, 0]);

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(GetBookingQuoteQuery.SeatIds));
    }

    [Fact]
    public void Validator_RejectsDuplicateSeatIds()
    {
        var query = new GetBookingQuoteQuery(1, [10, 12, 10]);

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(GetBookingQuoteQuery.SeatIds));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    public void Validator_AcceptsBoundaryValidSeatCounts(int count)
    {
        var seats = Enumerable.Range(1, count).ToList();
        var query = new GetBookingQuoteQuery(1, seats);

        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }
}
