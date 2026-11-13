using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace AirlineReservation.Infrastructure.Repositories;

public class SeatHoldRepository : ISeatHoldRepository
{
    private readonly AppDbContext _context;

    public SeatHoldRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<SeatHold>> GetActiveHoldsByFlightIdAsync(
        int flightId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return await _context.SeatHolds
            .Where(h => h.FlightId == flightId && h.ExpiresAtUtc > utcNow)
            .Include(h => h.Seat)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SeatHold>> GetUserActiveHoldsForFlightAsync(
        int flightId,
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return await _context.SeatHolds
            .Where(h => h.FlightId == flightId && h.UserId == userId && h.ExpiresAtUtc > utcNow)
            .Include(h => h.Seat)
            .OrderBy(h => h.Seat.RowNumber)
            .ThenBy(h => h.Seat.ColumnLetter)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SeatHold>> GetUserActiveHoldsAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return await _context.SeatHolds
            .Where(h => h.UserId == userId && h.ExpiresAtUtc > utcNow)
            .Include(h => h.Seat)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<int>> GetActiveHeldSeatIdsForFlightAsync(
        int flightId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var ids = await _context.SeatHolds
            .Where(h => h.FlightId == flightId && h.ExpiresAtUtc > utcNow)
            .Select(h => h.SeatId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    public async Task<int> ReleaseUserHoldsForFlightAsync(
        int flightId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.SeatHolds
            .Where(h => h.FlightId == flightId && h.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> ReleaseOtherFlightHoldsForUserAsync(
        int currentFlightId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.SeatHolds
            .Where(h => h.UserId == userId && h.FlightId != currentFlightId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteExpiredHoldsForSeatsAsync(
        int flightId,
        IReadOnlyCollection<int> seatIds,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        if (seatIds.Count == 0) return 0;

        return await _context.SeatHolds
            .Where(h => h.FlightId == flightId && seatIds.Contains(h.SeatId) && h.ExpiresAtUtc <= utcNow)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteAllExpiredHoldsAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return await _context.SeatHolds
            .Where(h => h.ExpiresAtUtc <= utcNow)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task AddRangeAsync(
        IEnumerable<SeatHold> holds,
        CancellationToken cancellationToken = default)
    {
        await _context.SeatHolds.AddRangeAsync(holds, cancellationToken);
    }
}
