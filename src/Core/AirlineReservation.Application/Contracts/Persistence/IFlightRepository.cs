using AirlineReservation.Domain.Entities;

namespace AirlineReservation.Application.Contracts.Persistence;

/// <summary>
/// Flight-specific repository contract for search and detail queries.
/// </summary>
public interface IFlightRepository : IGenericRepository<Flight>
{
    Task<IReadOnlyList<Flight>> SearchFlightsAsync(
        int departureAirportId,
        int arrivalAirportId,
        DateTime departureDate,
        CancellationToken cancellationToken = default);

    Task<Flight?> GetFlightWithDetailsAsync(int flightId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Flight>> GetUpcomingFlightsAsync(int count = 10, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Flight>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default);
}
