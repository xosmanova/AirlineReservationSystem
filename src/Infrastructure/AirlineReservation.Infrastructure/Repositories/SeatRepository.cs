using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using AirlineReservation.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace AirlineReservation.Infrastructure.Repositories;

/// <summary>
/// Specialized repository implementation for Seat and cabin layout operations.
/// </summary>
public class SeatRepository : GenericRepository<Seat>, ISeatRepository
{
    public SeatRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Seat>> GetSeatsByAircraftIdAsync(int aircraftId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(s => s.AircraftId == aircraftId)
            .OrderBy(s => s.RowNumber)
            .ThenBy(s => s.ColumnLetter)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetOccupiedSeatIdsForFlightAsync(int flightId, CancellationToken cancellationToken = default)
    {
        // A seat is occupied only if its ticket is active (not deleted, not cancelled) and its booking is not Cancelled or Expired
        return await _context.Tickets
            .Include(t => t.Booking)
            .Where(t => t.FlightId == flightId
                        && !t.IsCancelled
                        && t.Booking.Status != BookingStatus.Cancelled
                        && t.Booking.Status != BookingStatus.Expired)
            .Select(t => t.SeatId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Seat>> GetSeatsWithStatusForFlightAsync(int flightId, int aircraftId, CancellationToken cancellationToken = default)
    {
        // Return full aircraft cabin seat list for the flight
        return await _dbSet
            .Where(s => s.AircraftId == aircraftId)
            .OrderBy(s => s.RowNumber)
            .ThenBy(s => s.ColumnLetter)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
