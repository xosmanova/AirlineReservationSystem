using FluentValidation;

namespace AirlineReservation.Application.Features.Airports.Queries.SearchAirports;

/// <summary>
/// Validator for airport search query parameters.
/// </summary>
public class SearchAirportsQueryValidator : AbstractValidator<SearchAirportsQuery>
{
    public SearchAirportsQueryValidator()
    {
        RuleFor(x => x.Search)
            .MaximumLength(50).WithMessage("Airport search term must not exceed 50 characters.");

        RuleFor(x => x.Limit)
            .GreaterThan(0).WithMessage("Limit must be greater than 0.")
            .LessThanOrEqualTo(50).WithMessage("Limit cannot exceed 50.");
    }
}
