using AirlineReservation.Application.DTOs.Flight;
using AirlineReservation.Domain.Entities;

namespace AirlineReservation.Application.Contracts.Persistence;

/// <summary>
/// Flight-specific repository contract for search and detail queries.
/// </summary>
public interface IFlightRepository : IGenericRepository<Flight>
{
    Task<(IReadOnlyList<FlightDto> Items, int TotalCount)> SearchFlightsAsync(
        int departureAirportId,
        int arrivalAirportId,
        DateTime startUtc,
        DateTime endUtc,
        DateTime nowUtc,
        int passengersCount = 1,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        int? airlineId = null,
        int? maxStops = null,
        string? sortBy = null,
        string? sortDirection = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    Task<Flight?> GetFlightWithDetailsAsync(int flightId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Flight>> GetUpcomingFlightsAsync(int count = 10, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Flight>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default);
}
