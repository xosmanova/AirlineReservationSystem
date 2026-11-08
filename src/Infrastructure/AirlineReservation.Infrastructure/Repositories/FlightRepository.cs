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
        decimal? maxPrice = null,
        int? airlineId = null,
        int? maxStops = null,
        string? sortBy = null,
        string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        var targetDate = departureDate.Date;
        var nextDate = targetDate.AddDays(1);

        // Uses composite index IX_Flights_DepartureAirportId_ArrivalAirportId_DepartureTimeUtc
        var query = _dbSet
            .Include(f => f.Airline)
            .Include(f => f.Aircraft)
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .Include(f => f.Tickets)
                .ThenInclude(t => t.Booking)
            .Where(f => f.DepartureAirportId == departureAirportId
                        && f.ArrivalAirportId == arrivalAirportId
                        && f.DepartureTimeUtc >= targetDate
                        && f.DepartureTimeUtc < nextDate
                        && f.Status == FlightStatus.Scheduled);

        // Optional filter: Max price
        if (maxPrice.HasValue && maxPrice.Value > 0)
        {
            query = query.Where(f => f.BasePrice <= maxPrice.Value);
        }

        // Optional filter: Airline ID
        if (airlineId.HasValue && airlineId.Value > 0)
        {
            query = query.Where(f => f.AirlineId == airlineId.Value);
        }

        // Optional filter: Max stops (0 = direct only, 1 = up to 1 stop)
        if (maxStops.HasValue && maxStops.Value >= 0)
        {
            query = query.Where(f => f.Stops <= maxStops.Value);
        }

        // Sorting
        bool isDescending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        query = sortBy?.ToLowerInvariant() switch
        {
            "price" => isDescending ? query.OrderByDescending(f => f.BasePrice) : query.OrderBy(f => f.BasePrice),
            _ => isDescending ? query.OrderByDescending(f => f.DepartureTimeUtc) : query.OrderBy(f => f.DepartureTimeUtc)
        };

        return await query.AsNoTracking().ToListAsync(cancellationToken);
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
                .ThenInclude(t => t.Booking)
            .FirstOrDefaultAsync(f => f.Id == flightId, cancellationToken);
    }

    public async Task<IReadOnlyList<Flight>> GetUpcomingFlightsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(f => f.Airline)
            .Include(f => f.Aircraft)
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .Where(f => f.DepartureTimeUtc >= DateTime.UtcNow
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
            .OrderByDescending(f => f.DepartureTimeUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
