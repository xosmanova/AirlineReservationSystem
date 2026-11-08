using System.Linq.Expressions;
using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Mappings;
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
    private readonly AirlineMappingProfile _mapper = new();

    private static (Flight, Aircraft) CreateSampleFlight(int flightId = 1, int totalSeats = 142)
    {
        var gyd = new Airport { Id = 1, IataCode = "GYD", Name = "Heydar Aliyev Int.", City = "Baku", Country = "Azerbaijan" };
        var ist = new Airport { Id = 2, IataCode = "IST", Name = "Istanbul Airport", City = "Istanbul", Country = "Turkey" };
        var azal = new Airline { Id = 1, Code = "J2", Name = "Azerbaijan Airlines", LogoUrl = "https://azal.az/logo.png" };
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
        var (flight, aircraft) = CreateSampleFlight(totalSeats: 142);
        var fakeRepo = new FakeFlightRepository([flight]);
        var fakeUow = new FakeUnitOfWork(fakeRepo);
        var handler = new SearchFlightsQueryHandler(fakeUow, _mapper);

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
        Assert.Single(result.Data);

        var flightDto = result.Data[0];
        Assert.Equal("J2101", flightDto.FlightNumber);
        Assert.Equal(142, flightDto.TotalSeats);
        // Total (142) - 2 confirmed tickets = 140 available seats!
        Assert.Equal(140, flightDto.AvailableSeatsCount);
        Assert.Equal("Airbus A320-200", flightDto.AircraftModel);
        Assert.Equal(180, flightDto.DurationMinutes);
    }

    [Fact]
    public async Task Handle_WhenAvailableSeatsLessThanRequestedPassengers_FiltersOutFlight()
    {
        // Arrange: aircraft with 5 total seats, 2 occupied -> 3 available
        var (flight, _) = CreateSampleFlight(totalSeats: 5);
        var fakeRepo = new FakeFlightRepository([flight]);
        var fakeUow = new FakeUnitOfWork(fakeRepo);
        var handler = new SearchFlightsQueryHandler(fakeUow, _mapper);

        // Requesting 4 passengers when only 3 are available
        var query = new SearchFlightsQuery(
            DepartureAirportId: 1,
            ArrivalAirportId: 2,
            DepartureDate: new DateTime(2026, 11, 10),
            PassengersCount: 4
        );

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
        Assert.Contains("No available flights found", result.Message);
    }

    [Fact]
    public async Task GetFlightById_WhenFlightExists_ReturnsFlightWithAvailableSeats()
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
        Assert.Equal(152, result.Data.AvailableSeatsCount); // 154 - 2 confirmed
    }

    [Fact]
    public async Task GetFlightById_WhenFlightNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var fakeRepo = new FakeFlightRepository([]);
        var fakeUow = new FakeUnitOfWork(fakeRepo);
        var handler = new GetFlightByIdQueryHandler(fakeUow, _mapper);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetFlightByIdQuery(999), CancellationToken.None));
    }

    #region Fakes for Unit Testing

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public IFlightRepository Flights { get; }
        public IBookingRepository Bookings => throw new NotImplementedException();
        public ISeatRepository Seats => throw new NotImplementedException();

        public FakeUnitOfWork(IFlightRepository flightRepository)
        {
            Flights = flightRepository;
        }

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

        public Task<IReadOnlyList<Flight>> SearchFlightsAsync(
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
            var query = _flights.Where(f =>
                f.DepartureAirportId == departureAirportId &&
                f.ArrivalAirportId == arrivalAirportId &&
                f.DepartureTimeUtc.Date == targetDate);

            if (maxPrice.HasValue && maxPrice.Value > 0)
                query = query.Where(f => f.BasePrice <= maxPrice.Value);

            if (airlineId.HasValue && airlineId.Value > 0)
                query = query.Where(f => f.AirlineId == airlineId.Value);

            if (maxStops.HasValue && maxStops.Value >= 0)
                query = query.Where(f => f.Stops <= maxStops.Value);

            return Task.FromResult<IReadOnlyList<Flight>>(query.ToList());
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

        public Task AddRangeAsync(IEnumerable<Flight> entities, CancellationToken cancellationToken = default)
        {
            _flights.AddRange(entities);
            return Task.CompletedTask;
        }

        public void Update(Flight entity) { }
        public void Delete(Flight entity) => _flights.Remove(entity);
        public void SoftDelete(Flight entity) { entity.IsDeleted = true; }
        public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_flights.Any(f => f.Id == id));
        public Task<int> CountAsync(Expression<Func<Flight, bool>>? predicate = null, CancellationToken cancellationToken = default)
            => Task.FromResult(predicate == null ? _flights.Count : _flights.AsQueryable().Count(predicate));
    }

    #endregion
}
