using System.Linq.Expressions;
using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Common.Services;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Flight;
using AirlineReservation.Application.Features.Bookings.Queries.GetBookingQuote;
using AirlineReservation.Domain.Common;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;

namespace AirlineReservation.Application.Tests.Features.Bookings;

public class GetBookingQuoteQueryHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static GetBookingQuoteQueryHandler CreateHandler(
        FlightSeatMapData? seatMapData,
        IReadOnlyList<Seat>? allSeats = null,
        PricingSettings? pricingSettings = null)
    {
        var clock = new FakeClock(FixedUtcNow);
        var pricingService = new PricingService(pricingSettings ?? new PricingSettings(), clock);
        var uow = new FakeUnitOfWork(
            new FakeFlightRepository(seatMapData),
            new FakeSeatRepository(allSeats ?? []));

        return new GetBookingQuoteQueryHandler(uow, pricingService, clock);
    }

    private static FlightSeatMapData CreateSeatMapData(
        FlightStatus status = FlightStatus.Scheduled,
        int hoursUntilDeparture = 240,
        decimal basePrice = 100.00m,
        IReadOnlySet<int>? occupiedSeatIds = null)
    {
        var seats = new List<SeatMapSeatItem>
        {
            new(11, 1, "1A", 1, "A", SeatClass.Business, 2.0m, true, false, false),
            new(12, 1, "12A", 12, "A", SeatClass.Economy, 1.0m, true, false, false),
            new(13, 1, "12B", 12, "B", SeatClass.Economy, 1.0m, false, false, false),
            new(14, 1, "12C", 12, "C", SeatClass.Economy, 1.0m, false, true, false)
        };

        return new FlightSeatMapData(
            FlightId: 100,
            FlightNumber: "J2100",
            AirlineName: "AZAL",
            AircraftId: 1,
            AircraftModel: "Boeing 777-300ER",
            BasePrice: basePrice,
            DepartureTimeUtc: FixedUtcNow.UtcDateTime.AddHours(hoursUntilDeparture),
            Status: status,
            Seats: seats,
            OccupiedSeatIds: occupiedSeatIds ?? new HashSet<int>());
    }

    [Fact]
    public async Task Handle_ValidSeats_ReturnsItemizedBreakdownAndExactTotal()
    {
        // Outside 72h window: Base 100.00m
        // Seat 1A: 100*2.0 + 10 (Window) = 210.00
        // Seat 12C: 100*1.0 + 5 (Aisle) = 105.00
        // Total = 315.00
        var handler = CreateHandler(CreateSeatMapData(hoursUntilDeparture: 240));

        var result = await handler.Handle(new GetBookingQuoteQuery(100, [11, 14]), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(315.00m, result.Data.TotalPrice);
        Assert.Equal(2, result.Data.Items.Count);
        Assert.Equal(210.00m, result.Data.Items[0].FinalPrice);
        Assert.Equal(105.00m, result.Data.Items[1].FinalPrice);
        Assert.False(result.Data.Items[0].IsLastMinuteDemandApplied);
    }

    [Fact]
    public async Task Handle_QuoteTotalEqualsSumOfRoundedLinePrices()
    {
        // 10.02 * 1.25 = 12.525 -> rounded per line = 12.53
        // Two identical seats => 12.53 + 12.53 = 25.06 (not round(25.05) = 25.05)
        var settings = new PricingSettings
        {
            WindowFee = 0m,
            AisleFee = 0m,
            LastMinuteDays = 3,
            LastMinuteMultiplier = 1.25m
        };
        var handler = CreateHandler(
            CreateSeatMapData(hoursUntilDeparture: 24, basePrice: 10.02m),
            pricingSettings: settings);

        var result = await handler.Handle(new GetBookingQuoteQuery(100, [12, 13]), CancellationToken.None);

        Assert.NotNull(result.Data);
        Assert.All(result.Data.Items, item => Assert.Equal(12.53m, item.FinalPrice));
        Assert.Equal(25.06m, result.Data.TotalPrice);
    }

    [Fact]
    public async Task Handle_DelayedFutureFlight_IsSellableAndReturnsQuote()
    {
        var handler = CreateHandler(CreateSeatMapData(status: FlightStatus.Delayed, hoursUntilDeparture: 48));

        var result = await handler.Handle(new GetBookingQuoteQuery(100, [13]), CancellationToken.None);

        Assert.True(result.Success);
        // Last minute window <= 72h: 100 * 1.0 * 1.20 = 120.00
        Assert.Equal(120.00m, result.Data!.TotalPrice);
        Assert.True(result.Data.Items[0].IsLastMinuteDemandApplied);
    }

    [Fact]
    public async Task Handle_UnknownFlight_ThrowsNotFoundException()
    {
        var handler = CreateHandler(seatMapData: null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetBookingQuoteQuery(999, [11]), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownSeatId_ThrowsNotFoundException()
    {
        var handler = CreateHandler(CreateSeatMapData(), allSeats: []);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetBookingQuoteQuery(100, [99999]), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SeatFromDifferentAircraft_ThrowsBadRequestException()
    {
        var foreignSeat = new Seat
        {
            Id = 500,
            AircraftId = 2,
            SeatNumber = "5A",
            RowNumber = 5,
            ColumnLetter = "A",
            SeatClass = SeatClass.Economy
        };
        var handler = CreateHandler(CreateSeatMapData(), allSeats: [foreignSeat]);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new GetBookingQuoteQuery(100, [500]), CancellationToken.None));
    }

    [Theory]
    [InlineData(FlightStatus.Cancelled, 240)]
    [InlineData(FlightStatus.Departed, 240)]
    [InlineData(FlightStatus.Landed, 240)]
    [InlineData(FlightStatus.Boarding, 240)]
    [InlineData(FlightStatus.Scheduled, 0)]
    [InlineData(FlightStatus.Scheduled, -1)]
    public async Task Handle_NonSellableFlight_ThrowsConflictException(FlightStatus status, int hoursUntilDeparture)
    {
        var handler = CreateHandler(CreateSeatMapData(status: status, hoursUntilDeparture: hoursUntilDeparture));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new GetBookingQuoteQuery(100, [11]), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_OccupiedSeats_ThrowsConflictExceptionListingSeatNumbers()
    {
        var handler = CreateHandler(CreateSeatMapData(occupiedSeatIds: new HashSet<int> { 12, 14 }));

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new GetBookingQuoteQuery(100, [11, 12, 14]), CancellationToken.None));

        Assert.Contains("12A", ex.Message);
        Assert.Contains("12C", ex.Message);
    }

    #region Fakes

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public IFlightRepository Flights { get; }
        public IBookingRepository Bookings => throw new NotImplementedException();
        public ISeatRepository Seats { get; }
        public IAirportRepository Airports => throw new NotImplementedException();
        public IAirlineRepository Airlines => throw new NotImplementedException();

        public FakeUnitOfWork(IFlightRepository flights, ISeatRepository seats)
        {
            Flights = flights;
            Seats = seats;
        }

        public IGenericRepository<T> Repository<T>() where T : BaseEntity => throw new NotImplementedException();
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeFlightRepository : IFlightRepository
    {
        private readonly FlightSeatMapData? _data;

        public FakeFlightRepository(FlightSeatMapData? data) => _data = data;

        public Task<FlightSeatMapData?> GetFlightSeatMapDataAsync(int flightId, CancellationToken ct = default)
            => Task.FromResult(_data?.FlightId == flightId ? _data : null);

        public Task<(IReadOnlyList<FlightDto> Items, int TotalCount)> SearchFlightsAsync(int d, int a, DateTime s, DateTime e, DateTime now, int pc = 1, decimal? min = null, decimal? max = null, int? al = null, int? st = null, string? sb = null, string? sd = null, int p = 1, int ps = 10, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Flight?> GetFlightWithDetailsAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Flight>> GetUpcomingFlightsAsync(int count = 10, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Flight>> GetAllWithDetailsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Flight?> GetByIdAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Flight>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Flight>> FindAsync(Expression<Func<Flight, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Flight?> FirstOrDefaultAsync(Expression<Func<Flight, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> AnyAsync(Expression<Func<Flight, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> CountAsync(Expression<Func<Flight, bool>>? predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Flight> AddAsync(Flight entity, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddRangeAsync(IEnumerable<Flight> entities, CancellationToken ct = default) => throw new NotImplementedException();
        public void Update(Flight entity) { }
        public void Delete(Flight entity) { }
        public void DeleteRange(IEnumerable<Flight> entities) { }
    }

    private sealed class FakeSeatRepository : ISeatRepository
    {
        private readonly IReadOnlyList<Seat> _seats;

        public FakeSeatRepository(IReadOnlyList<Seat> seats) => _seats = seats;

        public Task<IReadOnlyList<Seat>> FindAsync(Expression<Func<Seat, bool>> predicate, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Seat>>(_seats.Where(predicate.Compile()).ToList());

        public Task<IReadOnlyList<Seat>> GetSeatsByAircraftIdAsync(int aircraftId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<int>> GetOccupiedSeatIdsForFlightAsync(int flightId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Seat>> GetSeatsWithStatusForFlightAsync(int flightId, int aircraftId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Seat?> GetByIdAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Seat>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Seat?> FirstOrDefaultAsync(Expression<Func<Seat, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> AnyAsync(Expression<Func<Seat, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> CountAsync(Expression<Func<Seat, bool>>? predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Seat> AddAsync(Seat entity, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddRangeAsync(IEnumerable<Seat> entities, CancellationToken ct = default) => throw new NotImplementedException();
        public void Update(Seat entity) { }
        public void Delete(Seat entity) { }
        public void DeleteRange(IEnumerable<Seat> entities) { }
    }

    #endregion
}
