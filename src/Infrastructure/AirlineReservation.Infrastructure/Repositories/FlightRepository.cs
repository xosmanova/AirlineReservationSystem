using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using AirlineReservation.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace AirlineReservation.Infrastructure.Repositories;

/// <summary>
/// Specialized repository implementation for Flight operations.
/// </summary>
public class FlightRepository : GenericRepository<Flight>, IFlightRepository
{
    public FlightRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Flight>> SearchFlightsAsync(
        int departureAirportId,
        int arrivalAirportId,
        DateTime departureDate,
        CancellationToken cancellationToken = default)
    {
        var targetDate = departureDate.Date;

        return await _dbSet
            .Include(f => f.Airline)
            .Include(f => f.Aircraft)
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .Where(f => !f.IsDeleted
                        && f.DepartureAirportId == departureAirportId
                        && f.ArrivalAirportId == arrivalAirportId
                        && f.DepartureTimeUtc.Date == targetDate
                        && f.Status == FlightStatus.Scheduled)
            .OrderBy(f => f.DepartureTimeUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Flight?> GetFlightWithDetailsAsync(int flightId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(f => f.Airline)
            .Include(f => f.Aircraft)
                .ThenInclude(a => a.Seats)
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .Include(f => f.Tickets)
                .ThenInclude(t => t.Seat)
            .Include(f => f.Tickets)
                .ThenInclude(t => t.Passenger)
            .FirstOrDefaultAsync(f => f.Id == flightId && !f.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyList<Flight>> GetUpcomingFlightsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(f => f.Airline)
            .Include(f => f.Aircraft)
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .Where(f => !f.IsDeleted
                        && f.DepartureTimeUtc >= DateTime.UtcNow
                        && f.Status == FlightStatus.Scheduled)
            .OrderBy(f => f.DepartureTimeUtc)
            .Take(count)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Flight>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(f => f.Airline)
            .Include(f => f.Aircraft)
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .Where(f => !f.IsDeleted)
            .OrderByDescending(f => f.DepartureTimeUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
