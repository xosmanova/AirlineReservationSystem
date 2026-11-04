using AirlineReservation.Domain.Entities;

namespace AirlineReservation.Application.Contracts.Persistence;

/// <summary>
/// Seat-specific repository contract for cabin layout and seat reservation status.
/// </summary>
public interface ISeatRepository : IGenericRepository<Seat>
{
    Task<IReadOnlyList<Seat>> GetSeatsByAircraftIdAsync(int aircraftId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetOccupiedSeatIdsForFlightAsync(int flightId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Seat>> GetSeatsWithStatusForFlightAsync(int flightId, int aircraftId, CancellationToken cancellationToken = default);
}
