using AirlineReservation.Domain.Entities;

namespace AirlineReservation.Application.Contracts.Persistence;

/// <summary>
/// Repository contract for airport queries including autocomplete and localized alias search.
/// </summary>
public interface IAirportRepository : IGenericRepository<Airport>
{
    /// <summary>
    /// Searches airports by IATA code, city name, airport name, or alias (localized spellings).
    /// </summary>
    /// <param name="searchTerm">Search keyword or partial term (e.g. "GYD", "Baku", "Bakı", "Баку", "IST").</param>
    /// <param name="limit">Maximum number of results to return (default 10).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching active airports ordered by City then Name.</returns>
    Task<IReadOnlyList<Airport>> SearchAsync(string? searchTerm, int limit = 10, CancellationToken cancellationToken = default);
}
