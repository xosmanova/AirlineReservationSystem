using System.Linq.Expressions;
using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Flight;
using AirlineReservation.Application.Features.Flights.Queries.GetFlightSeatMap;
using AirlineReservation.Domain.Common;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using AirlineReservation.Domain.Specifications;
using Xunit;

namespace AirlineReservation.Application.Tests.Features.Flights;

public class GetFlightSeatMapQueryHandlerTests
{
    private static Flight CreateFlightWithSeats(int flightId = 1)
    {
        var azal = new Airline { Id = 1, Code = "J2", Name = "Azerbaijan Airlines (AZAL)" };
        var aircraft = new Aircraft { Id = 1, Model = "Airbus A320-200", TailNumber = "DEMO-A320-1", TotalSeats = 4, AirlineId = 1 };

        // 4 seats in row 1
        aircraft.Seats =
        [
            new Seat { Id = 101, AircraftId = 1, SeatNumber = "1A", RowNumber = 1, ColumnLetter = "A", SeatClass = SeatClass.Business, BasePriceMultiplier = 2.0m },
            new Seat { Id = 102, AircraftId = 1, SeatNumber = "1C", RowNumber = 1, ColumnLetter = "C", SeatClass = SeatClass.Business, BasePriceMultiplier = 2.0m },
            new Seat { Id = 103, AircraftId = 1, SeatNumber = "1D", RowNumber = 1, ColumnLetter = "D", SeatClass = SeatClass.Business, BasePriceMultiplier = 2.0m },
            new Seat { Id = 104, AircraftId = 1, SeatNumber = "1F", RowNumber = 1, ColumnLetter = "F", SeatClass = SeatClass.Business, BasePriceMultiplier = 2.0m }
        ];

        var flight = new Flight
        {
            Id = flightId,
            FlightNumber = "J2101",
            AirlineId = 1,
            Airline = azal,
            AircraftId = 1,
            Aircraft = aircraft,
            BasePrice = 100.00m,
            DepartureTimeUtc = DateTime.UtcNow.AddDays(2),
            ArrivalTimeUtc = DateTime.UtcNow.AddDays(2).AddHours(3),
            Status = FlightStatus.Scheduled
        };

        // Seat 102 is booked with Confirmed booking
        var booking = new Booking { Id = 1, UserId = Guid.NewGuid(), PnrCode = "ABC123", Status = BookingStatus.Confirmed, IsDeleted = false };
        flight.Tickets.Add(new Ticket { Id = 1, FlightId = flightId, SeatId = 102, IsCancelled = false, IsDeleted = false, Booking = booking });

        return flight;
    }

    [Fact]
    public async Task Handle_ExistingFlight_CalculatesAccuratePricesAndOccupancy()
    {
        // Arrange
        var flight = CreateFlightWithSeats(1);
        var fakeRepo = new FakeFlightRepository(flight);
        var fakeUow = new FakeUnitOfWork(fakeRepo);
        var handler = new GetFlightSeatMapQueryHandler(fakeUow);

        // Act
        var result = await handler.Handle(new GetFlightSeatMapQuery(1), CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("J2101", result.Data.FlightNumber);
        Assert.Equal("Airbus A320-200", result.Data.AircraftModel);
        Assert.Equal(4, result.Data.TotalSeats);
        Assert.Equal(1, result.Data.OccupiedSeatsCount);
        Assert.Equal(3, result.Data.AvailableSeatsCount);

        // Check seat 1A (available, 100 * 2.0 = 200)
        var seat1A = result.Data.Rows[0].Seats.First(s => s.SeatNumber == "1A");
        Assert.Equal(SeatStatus.Available, seat1A.Status);
        Assert.False(seat1A.HeldByMe);
        Assert.Equal(200.00m, seat1A.Price);

        // Check seat 1C (occupied)
        var seat1C = result.Data.Rows[0].Seats.First(s => s.SeatNumber == "1C");
        Assert.Equal(SeatStatus.Occupied, seat1C.Status);
        Assert.False(seat1C.HeldByMe);

        // Contract assertions: SvgMap and FlightSeatDto/IsOccupied must be absent
        Assert.Null(typeof(AirlineReservation.Application.DTOs.Seat.SeatMapDto).GetProperty("SvgMap"));
        Assert.Null(typeof(AirlineReservation.Application.DTOs.Seat.SeatDto).GetProperty("IsOccupied"));
        Assert.Null(typeof(AirlineReservation.Application.DTOs.Seat.SeatMapDto).Assembly.GetType("AirlineReservation.Application.DTOs.Seat.FlightSeatDto"));
    }

    [Fact]
    public async Task Handle_NonExistingFlight_ThrowsNotFoundException()
    {
        // Arrange
        var fakeRepo = new FakeFlightRepository(null);
        var fakeUow = new FakeUnitOfWork(fakeRepo);
        var handler = new GetFlightSeatMapQueryHandler(fakeUow);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetFlightSeatMapQuery(999), CancellationToken.None));
    }

    #region Fakes

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public IFlightRepository Flights { get; }
        public IBookingRepository Bookings => throw new NotImplementedException();
        public ISeatRepository Seats => throw new NotImplementedException();
        public IAirportRepository Airports => throw new NotImplementedException();
        public IAirlineRepository Airlines => throw new NotImplementedException();

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
        private readonly Flight? _flight;

        public FakeFlightRepository(Flight? flight)
        {
            _flight = flight;
        }

        public Task<Flight?> GetFlightWithDetailsAsync(int flightId, CancellationToken cancellationToken = default)
            => Task.FromResult(_flight?.Id == flightId ? _flight : null);

        public Task<FlightSeatMapData?> GetFlightSeatMapDataAsync(int flightId, CancellationToken cancellationToken = default)
        {
            if (_flight == null || _flight.Id != flightId)
            {
                return Task.FromResult<FlightSeatMapData?>(null);
            }

            var seats = (_flight.Aircraft?.Seats ?? [])
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
                .ToList();

            var occupied = _flight.Tickets
                .Where(TicketSpecifications.IsActiveCompiled)
                .Select(t => t.SeatId)
                .ToHashSet();

            return Task.FromResult<FlightSeatMapData?>(new FlightSeatMapData(
                _flight.Id,
                _flight.FlightNumber,
                _flight.Airline?.Name ?? string.Empty,
                _flight.AircraftId,
                _flight.Aircraft?.Model ?? string.Empty,
                _flight.BasePrice,
                _flight.DepartureTimeUtc,
                _flight.Status,
                seats,
                occupied));
        }

        public Task<(IReadOnlyList<FlightDto> Items, int TotalCount)> SearchFlightsAsync(int d, int a, DateTime st, DateTime en, DateTime now, int pc = 1, decimal? minP = null, decimal? maxP = null, int? al = null, int? stp = null, string? sb = null, string? sd = null, int p = 1, int ps = 10, CancellationToken ct = default)
            => Task.FromResult<(IReadOnlyList<FlightDto> Items, int TotalCount)>(([], 0));

        public Task<IReadOnlyList<Flight>> GetUpcomingFlightsAsync(int count = 10, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Flight>>([]);

        public Task<IReadOnlyList<Flight>> GetAllWithDetailsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Flight>>([]);

        public Task<Flight?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(_flight);
        public Task<IReadOnlyList<Flight>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Flight>>([]);
        public Task<IReadOnlyList<Flight>> FindAsync(Expression<Func<Flight, bool>> p, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Flight>>([]);
        public Task<Flight?> FirstOrDefaultAsync(Expression<Func<Flight, bool>> p, CancellationToken ct = default) => Task.FromResult(_flight);
        public Task<bool> AnyAsync(Expression<Func<Flight, bool>> p, CancellationToken ct = default) => Task.FromResult(_flight != null);
        public void DeleteRange(IEnumerable<Flight> entities) { }
        public Task<Flight> AddAsync(Flight entity, CancellationToken ct = default) => Task.FromResult(entity);
        public Task AddRangeAsync(IEnumerable<Flight> entities, CancellationToken ct = default) => Task.CompletedTask;
        public void Update(Flight entity) { }
        public void Delete(Flight entity) { }
        public void SoftDelete(Flight entity) { }
        public Task<bool> ExistsAsync(int id, CancellationToken ct = default) => Task.FromResult(_flight != null);
        public Task<int> CountAsync(Expression<Func<Flight, bool>>? p = null, CancellationToken ct = default) => Task.FromResult(_flight != null ? 1 : 0);
    }

    #endregion
}
