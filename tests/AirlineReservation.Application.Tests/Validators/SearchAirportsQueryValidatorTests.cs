using AirlineReservation.Application.Features.Airports.Queries.SearchAirports;
using Xunit;

namespace AirlineReservation.Application.Tests.Validators;

public class SearchAirportsQueryValidatorTests
{
    private readonly SearchAirportsQueryValidator _validator = new();

    [Fact]
    public void Validator_Passes_WhenQueryIsValid()
    {
        var query = new SearchAirportsQuery("Baku", 10);
        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validator_Passes_WhenSearchIsNull()
    {
        var query = new SearchAirportsQuery(null, 10);
        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validator_Fails_WhenSearchExceeds50Characters()
    {
        var query = new SearchAirportsQuery(new string('a', 51), 10);
        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SearchAirportsQuery.Search));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(51)]
    [InlineData(100)]
    public void Validator_Fails_WhenLimitIsOutOfRange(int invalidLimit)
    {
        var query = new SearchAirportsQuery("Baku", invalidLimit);
        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SearchAirportsQuery.Limit));
    }
}
