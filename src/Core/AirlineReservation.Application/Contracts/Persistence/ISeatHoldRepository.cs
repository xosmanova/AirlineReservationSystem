using AirlineReservation.Domain.Entities;

namespace AirlineReservation.Application.Contracts.Persistence;

/// <summary>
/// Persistence contract for database-backed temporary seat reservation holds.
/// </summary>
public interface ISeatHoldRepository
{
    Task<IReadOnlyList<SeatHold>> GetActiveHoldsByFlightIdAsync(int flightId, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SeatHold>> GetUserActiveHoldsForFlightAsync(int flightId, Guid userId, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SeatHold>> GetUserAllHoldsForFlightAsync(int flightId, Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SeatHold>>([]);

    Task<int> ClaimHoldsAsync(int flightId, Guid userId, IReadOnlyCollection<int> holdIds, CancellationToken cancellationToken = default)
        => Task.FromResult(holdIds.Count);
    Task<IReadOnlyList<SeatHold>> GetUserActiveHoldsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<int>> GetActiveHeldSeatIdsForFlightAsync(int flightId, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<int> ReleaseUserHoldsForFlightAsync(int flightId, Guid userId, CancellationToken cancellationToken = default);
    Task<int> ReleaseOtherFlightHoldsForUserAsync(int currentFlightId, Guid userId, CancellationToken cancellationToken = default);
    Task<int> DeleteExpiredHoldsForSeatsAsync(int flightId, IReadOnlyCollection<int> seatIds, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<int> DeleteAllExpiredHoldsAsync(DateTime utcNow, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<SeatHold> holds, CancellationToken cancellationToken = default);
}
