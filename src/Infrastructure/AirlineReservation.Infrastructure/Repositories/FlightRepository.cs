using AirlineReservation.Domain.Specifications;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Airline;
using AirlineReservation.Application.DTOs.Airport;
using AirlineReservation.Application.DTOs.Flight;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using AirlineReservation.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace AirlineReservation.Infrastructure.Repositories;

/// <summary>
/// Specialized repository implementation for Flight operations.
/// Utilizes composite index IX_Flights_DepartureAirportId_ArrivalAirportId_DepartureTimeUtc.
/// Uses optimized correlated subquery and projection to avoid memory overhead and N+1 queries.
/// </summary>
public class FlightRepository : GenericRepository<Flight>, IFlightRepository
{
    private static readonly FlightStatus[] SellableStatuses = [FlightStatus.Scheduled, FlightStatus.Delayed];

    public FlightRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<(IReadOnlyList<FlightDto> Items, int TotalCount)> SearchFlightsAsync(
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
        CancellationToken cancellationToken = default)
    {
        // Base query with sargable filters, sellable statuses (Scheduled + Delayed), and future departure
        var baseQuery = _dbSet
            .Where(f => f.DepartureAirportId == departureAirportId
                        && f.ArrivalAirportId == arrivalAirportId
                        && f.DepartureTimeUtc >= startUtc
                        && f.DepartureTimeUtc < endUtc
                        && f.DepartureTimeUtc > nowUtc
                        && SellableStatuses.Contains(f.Status));

        // Optional filter: Min price
        if (minPrice.HasValue && minPrice.Value >= 0)
        {
            baseQuery = baseQuery.Where(f => f.BasePrice >= minPrice.Value);
        }

        // Optional filter: Max price
        if (maxPrice.HasValue && maxPrice.Value > 0)
        {
            baseQuery = baseQuery.Where(f => f.BasePrice <= maxPrice.Value);
        }

        // Optional filter: Airline ID
        if (airlineId.HasValue && airlineId.Value > 0)
        {
            baseQuery = baseQuery.Where(f => f.AirlineId == airlineId.Value);
        }

        // Optional filter: Max stops (0 = direct only, 1 = up to 1 stop)
        if (maxStops.HasValue && maxStops.Value >= 0)
        {
            baseQuery = baseQuery.Where(f => f.Stops <= maxStops.Value);
        }

        // Correlated subquery in SQL: AvailableSeats = TotalSeats - active tickets (not deleted, not cancelled, booking not cancelled)
        // Applied BEFORE OFFSET/FETCH in SQL
        if (passengersCount > 0)
        {
            baseQuery = baseQuery.Where(f =>
                (f.Aircraft.TotalSeats - f.Tickets.AsQueryable().Count(TicketSpecifications.IsActive)) >= passengersCount);
        }

        // 1. TotalCount query executing in SQL with the EXACT SAME filters
        int totalCount = await baseQuery.CountAsync(cancellationToken);

        // Sorting (departure, price, duration)
        bool isDescending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        IOrderedQueryable<Flight> sortedQuery = sortBy?.ToLowerInvariant() switch
        {
            "price" => isDescending
                ? baseQuery.OrderByDescending(f => f.BasePrice).ThenBy(f => f.Id)
                : baseQuery.OrderBy(f => f.BasePrice).ThenBy(f => f.Id),
            "duration" => isDescending
                ? baseQuery.OrderByDescending(f => EF.Functions.DateDiffMinute(f.DepartureTimeUtc, f.ArrivalTimeUtc)).ThenBy(f => f.Id)
                : baseQuery.OrderBy(f => EF.Functions.DateDiffMinute(f.DepartureTimeUtc, f.ArrivalTimeUtc)).ThenBy(f => f.Id),
            _ => isDescending
                ? baseQuery.OrderByDescending(f => f.DepartureTimeUtc).ThenBy(f => f.Id)
                : baseQuery.OrderBy(f => f.DepartureTimeUtc).ThenBy(f => f.Id)
        };

        // Pagination
        IQueryable<Flight> pagedQuery = sortedQuery;
        if (page > 0 && pageSize > 0)
        {
            pagedQuery = sortedQuery.Skip((page - 1) * pageSize).Take(pageSize);
        }

        // 2. Projected data query with AsNoTracking (no Ticket entity tracking, zero N+1)
        var items = await pagedQuery
            .Select(f => new FlightDto
            {
                Id = f.Id,
                FlightNumber = f.FlightNumber,
                AirlineId = f.AirlineId,
                Airline = f.Airline != null ? new AirlineDto
                {
                    Id = f.Airline.Id,
                    Code = f.Airline.Code,
                    Name = f.Airline.Name,
                    LogoUrl = f.Airline.LogoUrl
                } : null,
                AircraftId = f.AircraftId,
                AircraftModel = f.Aircraft != null ? f.Aircraft.Model : string.Empty,
                TotalSeats = f.Aircraft != null ? f.Aircraft.TotalSeats : 0,
                AvailableSeatsCount = (f.Aircraft != null ? f.Aircraft.TotalSeats : 0) -
                    f.Tickets.AsQueryable().Count(TicketSpecifications.IsActive),
                DepartureAirportId = f.DepartureAirportId,
                DepartureAirport = f.DepartureAirport != null ? new AirportDto
                {
                    Id = f.DepartureAirport.Id,
                    IataCode = f.DepartureAirport.IataCode,
                    Name = f.DepartureAirport.Name,
                    City = f.DepartureAirport.City,
                    Country = f.DepartureAirport.Country,
                    Timezone = f.DepartureAirport.TimeZone
                } : null,
                ArrivalAirportId = f.ArrivalAirportId,
                ArrivalAirport = f.ArrivalAirport != null ? new AirportDto
                {
                    Id = f.ArrivalAirport.Id,
                    IataCode = f.ArrivalAirport.IataCode,
                    Name = f.ArrivalAirport.Name,
                    City = f.ArrivalAirport.City,
                    Country = f.ArrivalAirport.Country,
                    Timezone = f.ArrivalAirport.TimeZone
                } : null,
                DepartureTimeUtc = f.DepartureTimeUtc,
                ArrivalTimeUtc = f.ArrivalTimeUtc,
                Stops = f.Stops,
                BasePrice = f.BasePrice,
                Status = f.Status
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return (items, totalCount);
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

    public async Task<FlightSeatMapData?> GetFlightSeatMapDataAsync(int flightId, CancellationToken cancellationToken = default)
    {
        // SQL Command 1: Project flight header and aircraft seats via Select + AsNoTracking (no Include)
        var flightData = await _dbSet
            .AsNoTracking()
            .Where(f => f.Id == flightId)
            .Select(f => new
            {
                f.Id,
                f.FlightNumber,
                AirlineName = f.Airline != null ? f.Airline.Name : string.Empty,
                f.AircraftId,
                AircraftModel = f.Aircraft != null ? f.Aircraft.Model : string.Empty,
                f.BasePrice,
                f.DepartureTimeUtc,
                f.Status,
                Seats = f.Aircraft!.Seats
                    .OrderBy(s => s.RowNumber)
                    .ThenBy(s => s.ColumnLetter)
                    .Select(s => new SeatMapSeatItem(
                        s.Id,
                        s.AircraftId,
                        s.SeatNumber,
                        s.RowNumber,
                        s.ColumnLetter,
                        s.SeatClass,
                        s.BasePriceMultiplier,
                        s.IsWindow,
                        s.IsAisle,
                        s.IsExitRow))
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (flightData == null)
        {
            return null;
        }

        // SQL Command 2: Active occupied seat IDs translated directly from TicketSpecifications.IsActive Expression
        var occupiedSeatIds = await _context.Tickets
            .AsNoTracking()
            .Where(t => t.FlightId == flightId)
            .Where(TicketSpecifications.IsActive)
            .Select(t => t.SeatId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new FlightSeatMapData(
            flightData.Id,
            flightData.FlightNumber,
            flightData.AirlineName,
            flightData.AircraftId,
            flightData.AircraftModel,
            flightData.BasePrice,
            flightData.DepartureTimeUtc,
            flightData.Status,
            flightData.Seats,
            occupiedSeatIds.ToHashSet());
    }

    public async Task<IReadOnlyList<Flight>> GetUpcomingFlightsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Include(f => f.Airline)
            .Include(f => f.Aircraft)
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .Where(f => f.DepartureTimeUtc >= now && SellableStatuses.Contains(f.Status))
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
