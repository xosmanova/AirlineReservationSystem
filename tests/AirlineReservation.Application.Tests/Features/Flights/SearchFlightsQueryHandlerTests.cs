using System.Linq.Expressions;
using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Helpers;
using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.Common.Mappings;
using AirlineReservation.Application.Common.Services;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Flight;
using AirlineReservation.Application.Features.Flights.Queries.GetFlightById;
using AirlineReservation.Application.Features.Flights.Queries.SearchFlights;
using AirlineReservation.Domain.Common;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using Xunit;

namespace AirlineReservation.Application.Tests.Features.Flights;

public class SearchFlightsQueryHandlerTests
{
    private static readonly TimeZoneInfo BakuTimeZone = TimeZoneHelper.ResolveTimeZone("Asia/Baku");
    private readonly IBusinessTime _businessTime = new BusinessTimeService(TimeProvider.System, BakuTimeZone);
    private readonly AirlineMappingProfile _mapper = new();

    private static (Flight, Aircraft) CreateSampleFlight(int flightId = 1, int totalSeats = 142)
    {
        var gyd = new Airport { Id = 1, IataCode = "GYD", Name = "Heydar Aliyev Int.", City = "Baku", Country = "Azerbaijan" };
        var ist = new Airport { Id = 2, IataCode = "IST", Name = "Istanbul Airport", City = "Istanbul", Country = "Turkey" };
        var azal = new Airline { Id = 1, Code = "J2", Name = "Azerbaijan Airlines (AZAL)", LogoUrl = "https://azal.az/logo.png" };
        var aircraft = new Aircraft { Id = 1, Model = "Airbus A320-200", TailNumber = "DEMO-A320-1", TotalSeats = totalSeats, AirlineId = 1 };

        var flight = new Flight
        {
            Id = flightId,
            FlightNumber = "J2101",
            AirlineId = azal.Id,
            Airline = azal,
            AircraftId = aircraft.Id,
            Aircraft = aircraft,
            DepartureAirportId = gyd.Id,
            DepartureAirport = gyd,
            ArrivalAirportId = ist.Id,
            ArrivalAirport = ist,
            DepartureTimeUtc = new DateTime(2026, 11, 10, 8, 30, 0, DateTimeKind.Utc),
            ArrivalTimeUtc = new DateTime(2026, 11, 10, 11, 30, 0, DateTimeKind.Utc),
            BasePrice = 180.00m,
            Stops = 0,
            Status = FlightStatus.Scheduled
        };

        // Add 2 confirmed tickets
        var confirmedBooking = new Booking { Id = 1, PnrCode = "PNR001", Status = BookingStatus.Confirmed, IsDeleted = false };
        flight.Tickets.Add(new Ticket { Id = 1, FlightId = flightId, SeatId = 1, IsCancelled = false, IsDeleted = false, Booking = confirmedBooking });
        flight.Tickets.Add(new Ticket { Id = 2, FlightId = flightId, SeatId = 2, IsCancelled = false, IsDeleted = false, Booking = confirmedBooking });

        // Add 1 cancelled ticket (should not count as occupied)
        flight.Tickets.Add(new Ticket { Id = 3, FlightId = flightId, SeatId = 3, IsCancelled = true, IsDeleted = false, Booking = confirmedBooking });

        // Add 1 ticket with expired booking (should not count as occupied)
        var expiredBooking = new Booking { Id = 2, PnrCode = "PNR002", Status = BookingStatus.Expired, IsDeleted = false };
        flight.Tickets.Add(new Ticket { Id = 4, FlightId = flightId, SeatId = 4, IsCancelled = false, IsDeleted = false, Booking = expiredBooking });

        return (flight, aircraft);
    }

    [Fact]
    public async Task Handle_CalculatesAccurateAvailableSeats_AndReturnsMatchingFlights()
    {
        // Arrange
        var (flight, _) = CreateSampleFlight(totalSeats: 142);
        var fakeRepo = new FakeFlightRepository([flight]);
        var fakeUow = new FakeUnitOfWork(fakeRepo);
        var handler = new SearchFlightsQueryHandler(fakeUow, _businessTime, TimeProvider.System);

        var query = new SearchFlightsQuery(
            DepartureAirportId: 1,
            ArrivalAirportId: 2,
            DepartureDate: new DateTime(2026, 11, 10),
            PassengersCount: 2
        );

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.TotalCount);
        Assert.Single(result.Data.Items);

        var flightDto = result.Data.Items[0];
        Assert.Equal("J2101", flightDto.FlightNumber);
        Assert.Equal(142, flightDto.TotalSeats);
        Assert.Equal(140, flightDto.AvailableSeatsCount);
        Assert.Equal("Airbus A320-200", flightDto.AircraftModel);
        Assert.Equal(180, flightDto.DurationMinutes);
    }

    [Fact]
    public async Task Handle_WhenPassengersCountExceedsAvailableSeats_FiltersOutFlight()
    {
        // Arrange
        var (flight, _) = CreateSampleFlight(totalSeats: 142);
        var fakeRepo = new FakeFlightRepository([flight]);
        var fakeUow = new FakeUnitOfWork(fakeRepo);
        var handler = new SearchFlightsQueryHandler(fakeUow, _businessTime, TimeProvider.System);

        var query = new SearchFlightsQuery(
            DepartureAirportId: 1,
            ArrivalAirportId: 2,
            DepartureDate: new DateTime(2026, 11, 10),
            PassengersCount: 141
        );

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(0, result.Data.TotalCount);
        Assert.Empty(result.Data.Items);
    }

    [Fact]
    public async Task Handle_BakuMidnightAndUtcBoundary_CorrectlyMatchesFlightAt2000Utc()
    {
        // In Baku (UTC+4), 2026-11-10 begins at 2026-11-09 20:00:00 UTC!
        // A flight departing at 2026-11-09 20:00:00 UTC is at 00:00 Baku time on Nov 10.
        var (flight, _) = CreateSampleFlight(flightId: 99);
        flight.DepartureTimeUtc = new DateTime(2026, 11, 9, 20, 0, 0, DateTimeKind.Utc);
        flight.ArrivalTimeUtc = new DateTime(2026, 11, 9, 23, 0, 0, DateTimeKind.Utc);

        var fakeRepo = new FakeFlightRepository([flight]);
        var fakeUow = new FakeUnitOfWork(fakeRepo);
        var handler = new SearchFlightsQueryHandler(fakeUow, _businessTime, TimeProvider.System);

        var query = new SearchFlightsQuery(
            DepartureAirportId: 1,
            ArrivalAirportId: 2,
            DepartureDate: new DateTime(2026, 11, 10) // User searches for 2026-11-10 in Baku time
        );

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.TotalCount);
        Assert.Single(result.Data.Items);
        Assert.Equal(99, result.Data.Items[0].Id);
    }

    [Fact]
    public async Task Handle_GetFlightById_WhenFound_ReturnsEnrichedFlightDto()
    {
        // Arrange
        var (flight, _) = CreateSampleFlight(flightId: 42, totalSeats: 154);
        var fakeRepo = new FakeFlightRepository([flight]);
        var fakeUow = new FakeUnitOfWork(fakeRepo);
        var handler = new GetFlightByIdQueryHandler(fakeUow, _mapper);

        // Act
        var result = await handler.Handle(new GetFlightByIdQuery(42), CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(42, result.Data.Id);
        Assert.Equal(154, result.Data.TotalSeats);
        Assert.Equal(152, result.Data.AvailableSeatsCount);
    }

    [Fact]
    public async Task Handle_GetFlightById_WhenNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var fakeRepo = new FakeFlightRepository([]);
        var fakeUow = new FakeUnitOfWork(fakeRepo);
        var handler = new GetFlightByIdQueryHandler(fakeUow, _mapper);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetFlightByIdQuery(999), CancellationToken.None));
    }

    // --- Fakes for in-memory testing without DB dependency ---

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public FakeUnitOfWork(IFlightRepository flightRepo)
        {
            Flights = flightRepo;
            Bookings = null!;
            Seats = null!;
        }

        public IFlightRepository Flights { get; }
        public IBookingRepository Bookings { get; }
        public ISeatRepository Seats { get; }

        public IGenericRepository<T> Repository<T>() where T : BaseEntity => throw new NotImplementedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeFlightRepository : IFlightRepository
    {
        private readonly List<Flight> _flights;

        public FakeFlightRepository(IEnumerable<Flight> flights)
        {
            _flights = flights.ToList();
        }

        public Task<(IReadOnlyList<FlightDto> Items, int TotalCount)> SearchFlightsAsync(
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
            var query = _flights.Where(f =>
                f.DepartureAirportId == departureAirportId &&
                f.ArrivalAirportId == arrivalAirportId &&
                f.DepartureTimeUtc >= startUtc &&
                f.DepartureTimeUtc < endUtc &&
                f.Status == FlightStatus.Scheduled);

            if (minPrice.HasValue && minPrice.Value >= 0)
                query = query.Where(f => f.BasePrice >= minPrice.Value);

            if (maxPrice.HasValue && maxPrice.Value > 0)
                query = query.Where(f => f.BasePrice <= maxPrice.Value);

            if (airlineId.HasValue && airlineId.Value > 0)
                query = query.Where(f => f.AirlineId == airlineId.Value);

            if (maxStops.HasValue && maxStops.Value >= 0)
                query = query.Where(f => f.Stops <= maxStops.Value);

            var list = query.ToList();
            var dtos = new List<FlightDto>();
            foreach (var f in list)
            {
                int activeTickets = f.Tickets.Count(t => !t.IsDeleted && !t.IsCancelled && t.Booking != null && t.Booking.Status != BookingStatus.Cancelled && t.Booking.Status != BookingStatus.Expired);
                int totalSeats = f.Aircraft?.TotalSeats ?? 0;
                int availableSeats = totalSeats - activeTickets;

                if (passengersCount > 0 && availableSeats < passengersCount)
                    continue;

                dtos.Add(new FlightDto
                {
                    Id = f.Id,
                    FlightNumber = f.FlightNumber,
                    AirlineId = f.AirlineId,
                    AircraftId = f.AircraftId,
                    AircraftModel = f.Aircraft?.Model ?? "",
                    TotalSeats = totalSeats,
                    AvailableSeatsCount = availableSeats,
                    DepartureAirportId = f.DepartureAirportId,
                    ArrivalAirportId = f.ArrivalAirportId,
                    DepartureTimeUtc = f.DepartureTimeUtc,
                    ArrivalTimeUtc = f.ArrivalTimeUtc,
                    Stops = f.Stops,
                    BasePrice = f.BasePrice,
                    Status = f.Status
                });
            }

            return Task.FromResult<(IReadOnlyList<FlightDto> Items, int TotalCount)>((dtos, dtos.Count));
        }

        public Task<Flight?> GetFlightWithDetailsAsync(int flightId, CancellationToken cancellationToken = default)
        {
            var flight = _flights.FirstOrDefault(f => f.Id == flightId);
            return Task.FromResult(flight);
        }

        public Task<IReadOnlyList<Flight>> GetUpcomingFlightsAsync(int count = 10, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Flight>>(_flights.Take(count).ToList());

        public Task<IReadOnlyList<Flight>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Flight>>(_flights);

        public Task<Flight?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_flights.FirstOrDefault(f => f.Id == id));

        public Task<IReadOnlyList<Flight>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Flight>>(_flights);

        public Task<IReadOnlyList<Flight>> FindAsync(Expression<Func<Flight, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Flight>>(_flights.AsQueryable().Where(predicate).ToList());

        public Task<Flight?> FirstOrDefaultAsync(Expression<Func<Flight, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(_flights.AsQueryable().FirstOrDefault(predicate));

        public Task<bool> AnyAsync(Expression<Func<Flight, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(_flights.AsQueryable().Any(predicate));

        public void DeleteRange(IEnumerable<Flight> entities)
        {
            foreach (var entity in entities) _flights.Remove(entity);
        }

        public Task<Flight> AddAsync(Flight entity, CancellationToken cancellationToken = default)
        {
            _flights.Add(entity);
            return Task.FromResult(entity);
        }

                public Task<int> CountAsync(Expression<Func<Flight, bool>>? predicate = null, CancellationToken cancellationToken = default)
            => Task.FromResult(predicate == null ? _flights.Count : _flights.AsQueryable().Count(predicate));

        public Task AddRangeAsync(IEnumerable<Flight> entities, CancellationToken cancellationToken = default)
        {
            _flights.AddRange(entities);
            return Task.CompletedTask;
        }

        public void Update(Flight entity) { }
        public void Delete(Flight entity) => _flights.Remove(entity);

        public Task UpdateAsync(Flight entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Flight entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
