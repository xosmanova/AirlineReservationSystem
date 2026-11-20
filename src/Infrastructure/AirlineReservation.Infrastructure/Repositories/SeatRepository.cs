using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using AirlineReservation.Domain.Specifications;
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
        // Single source of truth: TicketSpecifications.IsActive
        return await _context.Tickets
            .Include(t => t.Booking)
            .Where(t => t.FlightId == flightId)
            .Where(TicketSpecifications.IsActive)
            .Select(t => t.SeatId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetActiveOccupiedSeatIdsForFlightAsync(
        int flightId,
        IReadOnlyCollection<int> seatIds,
        CancellationToken cancellationToken = default)
    {
        if (seatIds.Count == 0) return [];

        return await _context.Tickets
            .Include(t => t.Booking)
            .Where(t => t.FlightId == flightId && seatIds.Contains(t.SeatId))
            .Where(TicketSpecifications.IsActive)
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
