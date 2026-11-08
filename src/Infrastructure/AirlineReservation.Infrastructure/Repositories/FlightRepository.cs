using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using AirlineReservation.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace AirlineReservation.Infrastructure.Repositories;

/// <summary>
/// Specialized repository implementation for Flight operations.
/// Utilizes composite index IX_Flights_DepartureAirportId_ArrivalAirportId_DepartureTimeUtc.
/// Eager-loads all navigation properties to prevent N+1 query issues.
/// </summary>
public class FlightRepository : GenericRepository<Flight>, IFlightRepository
{
    public FlightRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Flight>> SearchFlightsAsync(
        int departureAirportId,
        int arrivalAirportId,
        DateTime startUtc,
        DateTime endUtc,
        DateTime nowUtc,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        int? airlineId = null,
        int? maxStops = null,
        string? sortBy = null,
        string? sortDirection = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        // Composite index sargable query with exact UTC range bounds
        // Excludes departed flights and cancelled flights
        var query = _dbSet
            .Include(f => f.Airline)
            .Include(f => f.Aircraft)
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .Include(f => f.Tickets)
                .ThenInclude(t => t.Booking)
            .Where(f => f.DepartureAirportId == departureAirportId
                        && f.ArrivalAirportId == arrivalAirportId
                        && f.DepartureTimeUtc >= startUtc
                        && f.DepartureTimeUtc < endUtc
                        && f.DepartureTimeUtc > nowUtc
                        && f.Status == FlightStatus.Scheduled);

        // Optional filter: Min price
        if (minPrice.HasValue && minPrice.Value >= 0)
        {
            query = query.Where(f => f.BasePrice >= minPrice.Value);
        }

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

        // Sorting (departure, price, duration)
        bool isDescending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        query = sortBy?.ToLowerInvariant() switch
        {
            "price" => isDescending ? query.OrderByDescending(f => f.BasePrice) : query.OrderBy(f => f.BasePrice),
            "duration" => isDescending
                ? query.OrderByDescending(f => EF.Functions.DateDiffMinute(f.DepartureTimeUtc, f.ArrivalTimeUtc))
                : query.OrderBy(f => EF.Functions.DateDiffMinute(f.DepartureTimeUtc, f.ArrivalTimeUtc)),
            _ => isDescending ? query.OrderByDescending(f => f.DepartureTimeUtc) : query.OrderBy(f => f.DepartureTimeUtc)
        };

        // Pagination
        if (page > 0 && pageSize > 0)
        {
            query = query.Skip((page - 1) * pageSize).Take(pageSize);
        }

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
        var now = DateTime.UtcNow;
        return await _dbSet
            .Include(f => f.Airline)
            .Include(f => f.Aircraft)
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .Where(f => f.DepartureTimeUtc >= now && f.Status == FlightStatus.Scheduled)
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
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
