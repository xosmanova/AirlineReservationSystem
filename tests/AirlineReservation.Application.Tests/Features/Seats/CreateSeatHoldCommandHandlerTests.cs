using System.Linq.Expressions;
using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Flight;
using AirlineReservation.Application.Features.Seats.Commands.CreateSeatHold;
using AirlineReservation.Domain.Common;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using Xunit;

namespace AirlineReservation.Application.Tests.Features.Seats;

public class CreateSeatHoldCommandHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    private sealed class FakeClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static FlightSeatMapData CreateFlightSeatMapData(
        int flightId = 100,
        FlightStatus status = FlightStatus.Scheduled,
        int hoursUntilDeparture = 48,
        IReadOnlySet<int>? occupiedSeatIds = null)
    {
        var seats = new List<SeatMapSeatItem>
        {
            new(101, 1, "1A", 1, "A", SeatClass.Business, 2.0m, true, false, false),
            new(102, 1, "1B", 1, "B", SeatClass.Business, 2.0m, false, false, false),
            new(103, 1, "1C", 1, "C", SeatClass.Business, 2.0m, false, true, false),
            new(104, 1, "2A", 2, "A", SeatClass.Economy, 1.0m, true, false, false),
            new(105, 1, "2B", 2, "B", SeatClass.Economy, 1.0m, false, false, false)
        };

        return new FlightSeatMapData(
            FlightId: flightId,
            FlightNumber: "J2100",
            AirlineName: "AZAL",
            AircraftId: 1,
            AircraftModel: "Airbus A320",
            BasePrice: 120.00m,
            DepartureTimeUtc: FixedUtcNow.UtcDateTime.AddHours(hoursUntilDeparture),
            Status: status,
            Seats: seats,
            OccupiedSeatIds: occupiedSeatIds ?? new HashSet<int>());
    }

    #region Test Fakes

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public IFlightRepository Flights { get; }
        public ISeatRepository Seats { get; }
        public ISeatHoldRepository SeatHolds { get; }
        public IBookingRepository Bookings => throw new NotImplementedException();
        public IAirportRepository Airports => throw new NotImplementedException();
        public IAirlineRepository Airlines => throw new NotImplementedException();

        public bool TransactionStarted { get; private set; }
        public bool TransactionCommitted { get; private set; }
        public bool TransactionRolledBack { get; private set; }
        public bool ThrowUniqueConstraintOnCommit { get; set; }

        public FakeUnitOfWork(
            IFlightRepository flightRepo,
            ISeatRepository seatRepo,
            ISeatHoldRepository seatHoldRepo)
        {
            Flights = flightRepo;
            Seats = seatRepo;
            SeatHolds = seatHoldRepo;
        }

        public Task BeginTransactionAsync(CancellationToken ct = default)
        {
            TransactionStarted = true;
            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken ct = default)
        {
            if (ThrowUniqueConstraintOnCommit)
            {
                TransactionRolledBack = true;
                throw new UniqueConstraintViolationException("Violation of UNIQUE KEY constraint.", null!);
            }

            TransactionCommitted = true;
            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken ct = default)
        {
            TransactionRolledBack = true;
            return Task.CompletedTask;
        }

        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
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

    private sealed class FakeSeatRepository(IReadOnlyList<Seat> seats) : ISeatRepository
    {
        public Task<IReadOnlyList<Seat>> FindAsync(Expression<Func<Seat, bool>> predicate, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Seat>>(seats.Where(predicate.Compile()).ToList());

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

    private sealed class FakeSeatHoldRepository : ISeatHoldRepository
    {
        public List<SeatHold> Holds { get; } = new();
        public int ReleasedOtherFlightHoldsCount { get; private set; }
        public int DeletedExpiredHoldsCount { get; private set; }

        public FakeSeatHoldRepository(IEnumerable<SeatHold>? initialHolds = null)
        {
            if (initialHolds != null) Holds.AddRange(initialHolds);
        }

        public Task<IReadOnlyList<SeatHold>> GetActiveHoldsByFlightIdAsync(int flightId, DateTime utcNow, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<SeatHold>>(
                Holds.Where(h => h.FlightId == flightId && h.ExpiresAtUtc > utcNow).ToList());
        }

        public Task<IReadOnlyList<SeatHold>> GetUserActiveHoldsForFlightAsync(int flightId, Guid userId, DateTime utcNow, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<SeatHold>>(
                Holds.Where(h => h.FlightId == flightId && h.UserId == userId && h.ExpiresAtUtc > utcNow).ToList());
        }

        public Task<IReadOnlyList<SeatHold>> GetUserActiveHoldsAsync(Guid userId, DateTime utcNow, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<SeatHold>>(
                Holds.Where(h => h.UserId == userId && h.ExpiresAtUtc > utcNow).ToList());
        }

        public Task<IReadOnlySet<int>> GetActiveHeldSeatIdsForFlightAsync(int flightId, DateTime utcNow, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlySet<int>>(
                Holds.Where(h => h.FlightId == flightId && h.ExpiresAtUtc > utcNow).Select(h => h.SeatId).ToHashSet());
        }

        public Task<int> ReleaseUserHoldsForFlightAsync(int flightId, Guid userId, CancellationToken ct = default)
        {
            int removed = Holds.RemoveAll(h => h.FlightId == flightId && h.UserId == userId);
            return Task.FromResult(removed);
        }

        public Task<int> ReleaseOtherFlightHoldsForUserAsync(int currentFlightId, Guid userId, CancellationToken ct = default)
        {
            int removed = Holds.RemoveAll(h => h.UserId == userId && h.FlightId != currentFlightId);
            ReleasedOtherFlightHoldsCount += removed;
            return Task.FromResult(removed);
        }

        public Task<int> DeleteExpiredHoldsForSeatsAsync(int flightId, IReadOnlyCollection<int> seatIds, DateTime utcNow, CancellationToken ct = default)
        {
            int removed = Holds.RemoveAll(h => h.FlightId == flightId && seatIds.Contains(h.SeatId) && h.ExpiresAtUtc <= utcNow);
            DeletedExpiredHoldsCount += removed;
            return Task.FromResult(removed);
        }

        public Task<int> DeleteAllExpiredHoldsAsync(DateTime utcNow, CancellationToken ct = default)
        {
            int removed = Holds.RemoveAll(h => h.ExpiresAtUtc <= utcNow);
            return Task.FromResult(removed);
        }

        public Task AddRangeAsync(IEnumerable<SeatHold> holds, CancellationToken ct = default)
        {
            Holds.AddRange(holds);
            return Task.CompletedTask;
        }
    }

    #endregion

    private static (CreateSeatHoldCommandHandler Handler, FakeUnitOfWork UnitOfWork, FakeSeatHoldRepository SeatHoldRepo) CreateSystem(
        FlightSeatMapData? seatMapData,
        IReadOnlyList<Seat>? allSeats = null,
        IEnumerable<SeatHold>? existingHolds = null)
    {
        var clock = new FakeClock(FixedUtcNow);
        var flightRepo = new FakeFlightRepository(seatMapData);
        var seatRepo = new FakeSeatRepository(allSeats ?? Array.Empty<Seat>());
        var holdRepo = new FakeSeatHoldRepository(existingHolds);
        var uow = new FakeUnitOfWork(flightRepo, seatRepo, holdRepo);
        var handler = new CreateSeatHoldCommandHandler(uow, clock, new HoldSettings { Minutes = 10 });

        return (handler, uow, holdRepo);
    }

    [Fact]
    public async Task Handle_FlightNotFound_ShouldThrowNotFoundException()
    {
        var (handler, _, _) = CreateSystem(seatMapData: null);

        var command = new CreateSeatHoldCommand(999, TestUserId, new[] { 101 });

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Theory]
    [InlineData(FlightStatus.Cancelled, 48)]
    [InlineData(FlightStatus.Departed, 48)]
    [InlineData(FlightStatus.Scheduled, -2)] // in the past
    public async Task Handle_NonSellableFlight_ShouldThrowConflictException(FlightStatus status, int hoursUntilDeparture)
    {
        var seatMapData = CreateFlightSeatMapData(status: status, hoursUntilDeparture: hoursUntilDeparture);
        var (handler, _, _) = CreateSystem(seatMapData);

        var command = new CreateSeatHoldCommand(100, TestUserId, new[] { 101 });

        var ex = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("not sellable", ex.Message);
    }

    [Fact]
    public async Task Handle_SeatNotOnAircraft_ShouldThrowBadRequestException()
    {
        var seatMapData = CreateFlightSeatMapData();
        var seatsInDb = new List<Seat>
        {
            new() { Id = 999, AircraftId = 2, SeatNumber = "99Z" }
        };
        var (handler, _, _) = CreateSystem(seatMapData, allSeats: seatsInDb);

        var command = new CreateSeatHoldCommand(100, TestUserId, new[] { 999 });

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("do not belong to flight", ex.Message);
    }

    [Fact]
    public async Task Handle_SeatNotExistingInDbAtAll_ShouldThrowNotFoundException()
    {
        var seatMapData = CreateFlightSeatMapData();
        var (handler, _, _) = CreateSystem(seatMapData, allSeats: Array.Empty<Seat>());

        var command = new CreateSeatHoldCommand(100, TestUserId, new[] { 999 });

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SeatOccupiedByTicket_ShouldThrowConflictException()
    {
        var seatMapData = CreateFlightSeatMapData(occupiedSeatIds: new HashSet<int> { 101 });
        var (handler, _, _) = CreateSystem(seatMapData);

        var command = new CreateSeatHoldCommand(100, TestUserId, new[] { 101 });

        var ex = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("already occupied", ex.Message);
    }

    [Fact]
    public async Task Handle_SeatHeldByOtherUser_ShouldThrowConflictException()
    {
        var seatMapData = CreateFlightSeatMapData();
        var existingHold = new SeatHold
        {
            Id = 1,
            FlightId = 100,
            SeatId = 101,
            UserId = OtherUserId,
            CreatedAtUtc = FixedUtcNow.UtcDateTime,
            ExpiresAtUtc = FixedUtcNow.UtcDateTime.AddMinutes(5)
        };
        var (handler, _, _) = CreateSystem(seatMapData, existingHolds: new[] { existingHold });

        var command = new CreateSeatHoldCommand(100, TestUserId, new[] { 101 });

        var ex = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("already held by another passenger", ex.Message);
    }

    [Fact]
    public async Task Handle_ValidSingleSeatHold_ShouldSucceedAndReturnRemainingSeconds()
    {
        var seatMapData = CreateFlightSeatMapData();
        var (handler, uow, holdRepo) = CreateSystem(seatMapData);

        var command = new CreateSeatHoldCommand(100, TestUserId, new[] { 101 });
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(100, result.Data.FlightId);
        Assert.Equal(600, result.Data.RemainingSeconds);
        Assert.Equal(FixedUtcNow.UtcDateTime.AddMinutes(10), result.Data.ExpiresAtUtc);
        Assert.Single(result.Data.HeldSeats);
        Assert.Equal(101, result.Data.HeldSeats[0].SeatId);
        Assert.Equal("1A", result.Data.HeldSeats[0].SeatNumber);
        Assert.Equal(600, result.Data.HeldSeats[0].RemainingSeconds);

        Assert.True(uow.TransactionStarted);
        Assert.True(uow.TransactionCommitted);
        Assert.False(uow.TransactionRolledBack);
        Assert.Single(holdRepo.Holds);
        Assert.Equal(101, holdRepo.Holds[0].SeatId);
        Assert.Equal(TestUserId, holdRepo.Holds[0].UserId);
    }

    [Fact]
    public async Task Handle_ValidMultiSeatHold_ShouldSucceedAndOrderSeatsProperly()
    {
        var seatMapData = CreateFlightSeatMapData();
        var (handler, _, holdRepo) = CreateSystem(seatMapData);

        // Requested out of order: 105 (2B), 101 (1A), 104 (2A)
        var command = new CreateSeatHoldCommand(100, TestUserId, new[] { 105, 101, 104 });
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data.HeldSeats.Count);
        // Should be ordered by RowNumber then ColumnLetter: 1A, 2A, 2B
        Assert.Equal("1A", result.Data.HeldSeats[0].SeatNumber);
        Assert.Equal("2A", result.Data.HeldSeats[1].SeatNumber);
        Assert.Equal("2B", result.Data.HeldSeats[2].SeatNumber);
        Assert.Equal(3, holdRepo.Holds.Count);
    }

    [Fact]
    public async Task Handle_ReSelectingOwnHeldSeats_ShouldReplaceHoldsWithoutConflict()
    {
        var seatMapData = CreateFlightSeatMapData();
        var existingHold = new SeatHold
        {
            Id = 1,
            FlightId = 100,
            SeatId = 101,
            UserId = TestUserId,
            CreatedAtUtc = FixedUtcNow.UtcDateTime.AddMinutes(-5),
            ExpiresAtUtc = FixedUtcNow.UtcDateTime.AddMinutes(5)
        };
        var (handler, _, holdRepo) = CreateSystem(seatMapData, existingHolds: new[] { existingHold });

        // User re-selects 101 and adds 102
        var command = new CreateSeatHoldCommand(100, TestUserId, new[] { 101, 102 });
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.HeldSeats.Count);
        // Expiry should be refreshed to full 10 minutes
        Assert.Equal(FixedUtcNow.UtcDateTime.AddMinutes(10), result.Data.ExpiresAtUtc);
        Assert.Equal(2, holdRepo.Holds.Count);
        Assert.All(holdRepo.Holds, h => Assert.Equal(TestUserId, h.UserId));
    }

    [Fact]
    public async Task Handle_HoldSpam_ShouldReleaseHoldsOnOtherFlights()
    {
        var seatMapData = CreateFlightSeatMapData(flightId: 100);
        var holdOnOtherFlight = new SeatHold
        {
            Id = 5,
            FlightId = 200, // Different flight!
            SeatId = 501,
            UserId = TestUserId,
            CreatedAtUtc = FixedUtcNow.UtcDateTime,
            ExpiresAtUtc = FixedUtcNow.UtcDateTime.AddMinutes(5)
        };
        var (handler, _, holdRepo) = CreateSystem(seatMapData, existingHolds: new[] { holdOnOtherFlight });

        var command = new CreateSeatHoldCommand(100, TestUserId, new[] { 101 });
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, holdRepo.ReleasedOtherFlightHoldsCount);
        Assert.DoesNotContain(holdRepo.Holds, h => h.FlightId == 200);
    }

    [Fact]
    public async Task Handle_HoldRule_ShouldDeleteExpiredHoldsBeforeAvailabilityCheck()
    {
        var seatMapData = CreateFlightSeatMapData();
        var expiredHoldByOther = new SeatHold
        {
            Id = 10,
            FlightId = 100,
            SeatId = 101,
            UserId = OtherUserId,
            CreatedAtUtc = FixedUtcNow.UtcDateTime.AddMinutes(-15),
            ExpiresAtUtc = FixedUtcNow.UtcDateTime.AddMinutes(-5) // Expired 5 mins ago
        };
        var (handler, _, holdRepo) = CreateSystem(seatMapData, existingHolds: new[] { expiredHoldByOther });

        var command = new CreateSeatHoldCommand(100, TestUserId, new[] { 101 });
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, holdRepo.DeletedExpiredHoldsCount);
        Assert.Single(holdRepo.Holds);
        Assert.Equal(TestUserId, holdRepo.Holds[0].UserId);
    }

    [Fact]
    public async Task Handle_UniqueConstraintViolationOnCommit_ShouldRollbackAndThrowConflictException()
    {
        var seatMapData = CreateFlightSeatMapData();
        var (handler, uow, _) = CreateSystem(seatMapData);
        uow.ThrowUniqueConstraintOnCommit = true;

        var command = new CreateSeatHoldCommand(100, TestUserId, new[] { 101 });

        var ex = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("held by another passenger", ex.Message);
        Assert.True(uow.TransactionRolledBack);
    }
}
