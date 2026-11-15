using System.Linq.Expressions;
using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Common.Services;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Flight;
using AirlineReservation.Application.Features.Flights.Queries.GetFlightSeatMap;
using AirlineReservation.Application.Features.Seats.Queries.GetFlightSeatStatus;
using AirlineReservation.Domain.Common;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using FluentValidation.TestHelper;
using Xunit;

namespace AirlineReservation.Application.Tests.Features.Seats;

public class GetFlightSeatStatusQueryTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    private sealed class FakeClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    #region Fakes

    private sealed class FakeUnitOfWork(IFlightRepository flights, ISeatHoldRepository holds) : IUnitOfWork
    {
        public IFlightRepository Flights => flights;
        public ISeatHoldRepository SeatHolds => holds;
        public ISeatRepository Seats => throw new NotImplementedException();
        public IBookingRepository Bookings => throw new NotImplementedException();
        public IAirportRepository Airports => throw new NotImplementedException();
        public IAirlineRepository Airlines => throw new NotImplementedException();

        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public IGenericRepository<T> Repository<T>() where T : BaseEntity => throw new NotImplementedException();
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeFlightRepository(FlightSeatMapData? data) : IFlightRepository
    {
        public Task<FlightSeatMapData?> GetFlightSeatMapDataAsync(int flightId, CancellationToken ct = default)
            => Task.FromResult(data?.FlightId == flightId ? data : null);

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

    private sealed class FakeSeatHoldRepository(IEnumerable<SeatHold> holds) : ISeatHoldRepository
    {
        private readonly List<SeatHold> _holds = holds.ToList();

        public Task<IReadOnlyList<SeatHold>> GetActiveHoldsByFlightIdAsync(int flightId, DateTime utcNow, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<SeatHold>>(
                _holds.Where(h => h.FlightId == flightId && h.ExpiresAtUtc > utcNow).ToList());
        }

        public Task<IReadOnlyList<SeatHold>> GetUserActiveHoldsForFlightAsync(int flightId, Guid userId, DateTime utcNow, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SeatHold>> GetUserActiveHoldsAsync(Guid userId, DateTime utcNow, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlySet<int>> GetActiveHeldSeatIdsForFlightAsync(int flightId, DateTime utcNow, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> ReleaseUserHoldsForFlightAsync(int flightId, Guid userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> ReleaseOtherFlightHoldsForUserAsync(int currentFlightId, Guid userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> DeleteExpiredHoldsForSeatsAsync(int flightId, IReadOnlyCollection<int> seatIds, DateTime utcNow, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> DeleteAllExpiredHoldsAsync(DateTime utcNow, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddRangeAsync(IEnumerable<SeatHold> holds, CancellationToken ct = default) => throw new NotImplementedException();
    }

    #endregion

    private static FlightSeatMapData CreateSampleSeatMapData(int flightId = 100)
    {
        var seats = new List<SeatMapSeatItem>
        {
            new(1, 1, "1A", 1, "A", SeatClass.Economy, 1.0m, true, false, false),
            new(2, 1, "1B", 1, "B", SeatClass.Economy, 1.0m, false, false, false),
            new(3, 1, "1C", 1, "C", SeatClass.Economy, 1.0m, false, true, false),
            new(4, 1, "2A", 2, "A", SeatClass.Economy, 1.0m, true, false, false)
        };

        // Seat 1 is occupied by ticket
        var occupiedSeatIds = new HashSet<int> { 1 };

        return new FlightSeatMapData(
            flightId,
            "J2100",
            "AZAL",
            1,
            "Airbus A320",
            100.00m,
            FixedUtcNow.UtcDateTime.AddDays(2),
            FlightStatus.Scheduled,
            seats,
            occupiedSeatIds);
    }

    [Fact]
    public void Validator_ValidQuery_ShouldPass()
    {
        var validator = new GetFlightSeatStatusQueryValidator();
        var result = validator.TestValidate(new GetFlightSeatStatusQuery(100));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validator_InvalidFlightId_ShouldFail(int flightId)
    {
        var validator = new GetFlightSeatStatusQueryValidator();
        var result = validator.TestValidate(new GetFlightSeatStatusQuery(flightId));
        result.ShouldHaveValidationErrorFor(x => x.FlightId);
    }

    [Fact]
    public async Task Handler_FlightNotFound_ShouldThrowNotFoundException()
    {
        var uow = new FakeUnitOfWork(new FakeFlightRepository(null), new FakeSeatHoldRepository([]));
        var handler = new GetFlightSeatStatusQueryHandler(uow, new FakeClock(FixedUtcNow));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetFlightSeatStatusQuery(999), CancellationToken.None));
    }

    [Fact]
    public async Task Handler_ShouldReturnOnlyNonAvailableSeatsAndGeneratedTimestamp()
    {
        var data = CreateSampleSeatMapData();
        // Seat 2 is held by TestUserId, Seat 3 is held by OtherUserId, Seat 4 is Available
        var holds = new List<SeatHold>
        {
            new() { Id = 10, FlightId = 100, SeatId = 2, UserId = TestUserId, ExpiresAtUtc = FixedUtcNow.UtcDateTime.AddMinutes(5) },
            new() { Id = 11, FlightId = 100, SeatId = 3, UserId = OtherUserId, ExpiresAtUtc = FixedUtcNow.UtcDateTime.AddMinutes(5) }
        };

        var clock = new FakeClock(FixedUtcNow);
        var uow = new FakeUnitOfWork(new FakeFlightRepository(data), new FakeSeatHoldRepository(holds));
        var handler = new GetFlightSeatStatusQueryHandler(uow, clock);

        var result = await handler.Handle(new GetFlightSeatStatusQuery(100, TestUserId), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(100, result.Data.FlightId);
        Assert.Equal(FixedUtcNow.UtcDateTime, result.Data.GeneratedAtUtc);

        // Exactly 3 seats returned: Seat 1 (Occupied), Seat 2 (Held, HeldByMe=true), Seat 3 (Held, HeldByMe=false).
        // Seat 4 is Available and MUST be omitted!
        Assert.Equal(3, result.Data.Seats.Count);
        Assert.DoesNotContain(result.Data.Seats, s => s.SeatId == 4);

        var seat1 = result.Data.Seats.First(s => s.SeatId == 1);
        Assert.Equal(SeatStatus.Occupied, seat1.Status);
        Assert.False(seat1.HeldByMe);

        var seat2 = result.Data.Seats.First(s => s.SeatId == 2);
        Assert.Equal(SeatStatus.Held, seat2.Status);
        Assert.True(seat2.HeldByMe);

        var seat3 = result.Data.Seats.First(s => s.SeatId == 3);
        Assert.Equal(SeatStatus.Held, seat3.Status);
        Assert.False(seat3.HeldByMe);
    }

    [Fact]
    public async Task Parity_FullSeatMap_And_StatusEndpoint_MustHaveIdenticalStatuses()
    {
        var data = CreateSampleSeatMapData();
        var holds = new List<SeatHold>
        {
            new() { Id = 10, FlightId = 100, SeatId = 2, UserId = TestUserId, ExpiresAtUtc = FixedUtcNow.UtcDateTime.AddMinutes(5) },
            new() { Id = 11, FlightId = 100, SeatId = 3, UserId = OtherUserId, ExpiresAtUtc = FixedUtcNow.UtcDateTime.AddMinutes(5) }
        };

        var clock = new FakeClock(FixedUtcNow);
        var uow = new FakeUnitOfWork(new FakeFlightRepository(data), new FakeSeatHoldRepository(holds));
        var pricingService = new PricingService(new PricingSettings(), clock);

        var mapHandler = new GetFlightSeatMapQueryHandler(uow, pricingService, clock);
        var statusHandler = new GetFlightSeatStatusQueryHandler(uow, clock);

        var mapResult = await mapHandler.Handle(new GetFlightSeatMapQuery(100, TestUserId), CancellationToken.None);
        var statusResult = await statusHandler.Handle(new GetFlightSeatStatusQuery(100, TestUserId), CancellationToken.None);

        var mapSeatsById = mapResult.Data!.Rows.SelectMany(r => r.Seats).ToDictionary(s => s.Id);
        var statusSeatsById = statusResult.Data!.Seats.ToDictionary(s => s.SeatId);

        // Every non-available seat in the map must match the status endpoint
        foreach (var (seatId, mapSeat) in mapSeatsById)
        {
            if (mapSeat.Status != SeatStatus.Available)
            {
                Assert.True(statusSeatsById.ContainsKey(seatId), $"Seat {seatId} ({mapSeat.Status}) should be in status delta.");
                var statusSeat = statusSeatsById[seatId];
                Assert.Equal(mapSeat.Status, statusSeat.Status);
                Assert.Equal(mapSeat.HeldByMe, statusSeat.HeldByMe);
            }
            else
            {
                Assert.False(statusSeatsById.ContainsKey(seatId), $"Available seat {seatId} should NOT be in status delta.");
            }
        }
    }
}
