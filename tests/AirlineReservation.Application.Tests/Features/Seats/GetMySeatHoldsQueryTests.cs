using System.Linq.Expressions;
using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Flight;
using AirlineReservation.Application.Features.Seats.Queries.GetMySeatHolds;
using AirlineReservation.Domain.Common;
using AirlineReservation.Domain.Entities;
using FluentValidation.TestHelper;
using Xunit;

namespace AirlineReservation.Application.Tests.Features.Seats;

public class GetMySeatHoldsQueryTests
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

    private sealed class FakeFlightRepository(Flight? flight) : IFlightRepository
    {
        public Task<Flight?> GetByIdAsync(int id, CancellationToken ct = default)
            => Task.FromResult(flight?.Id == id ? flight : null);

        public Task<FlightSeatMapData?> GetFlightSeatMapDataAsync(int flightId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(IReadOnlyList<FlightDto> Items, int TotalCount)> SearchFlightsAsync(int d, int a, DateTime s, DateTime e, DateTime now, int pc = 1, decimal? min = null, decimal? max = null, int? al = null, int? st = null, string? sb = null, string? sd = null, int p = 1, int ps = 10, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Flight?> GetFlightWithDetailsAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Flight>> GetUpcomingFlightsAsync(int count = 10, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Flight>> GetAllWithDetailsAsync(CancellationToken ct = default) => throw new NotImplementedException();
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

        public Task<IReadOnlyList<SeatHold>> GetUserActiveHoldsForFlightAsync(int flightId, Guid userId, DateTime utcNow, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<SeatHold>>(
                _holds.Where(h => h.FlightId == flightId && h.UserId == userId && h.ExpiresAtUtc > utcNow).ToList());
        }

        public Task<IReadOnlyList<SeatHold>> GetActiveHoldsByFlightIdAsync(int flightId, DateTime utcNow, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SeatHold>> GetUserActiveHoldsAsync(Guid userId, DateTime utcNow, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlySet<int>> GetActiveHeldSeatIdsForFlightAsync(int flightId, DateTime utcNow, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> ReleaseUserHoldsForFlightAsync(int flightId, Guid userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> ReleaseOtherFlightHoldsForUserAsync(int currentFlightId, Guid userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> DeleteExpiredHoldsForSeatsAsync(int flightId, IReadOnlyCollection<int> seatIds, DateTime utcNow, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> DeleteAllExpiredHoldsAsync(DateTime utcNow, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddRangeAsync(IEnumerable<SeatHold> holds, CancellationToken ct = default) => throw new NotImplementedException();
    }

    #endregion

    [Fact]
    public void Validator_ValidQuery_ShouldPass()
    {
        var validator = new GetMySeatHoldsQueryValidator();
        var result = validator.TestValidate(new GetMySeatHoldsQuery(100, TestUserId));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validator_InvalidFlightId_ShouldFail(int flightId)
    {
        var validator = new GetMySeatHoldsQueryValidator();
        var result = validator.TestValidate(new GetMySeatHoldsQuery(flightId, TestUserId));
        result.ShouldHaveValidationErrorFor(x => x.FlightId);
    }

    [Fact]
    public void Validator_EmptyUserId_ShouldFail()
    {
        var validator = new GetMySeatHoldsQueryValidator();
        var result = validator.TestValidate(new GetMySeatHoldsQuery(100, Guid.Empty));
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public async Task Handler_FlightNotFound_ShouldThrowNotFoundException()
    {
        var uow = new FakeUnitOfWork(new FakeFlightRepository(null), new FakeSeatHoldRepository(Array.Empty<SeatHold>()));
        var handler = new GetMySeatHoldsQueryHandler(uow, new FakeClock(FixedUtcNow));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetMySeatHoldsQuery(100, TestUserId), CancellationToken.None));
    }

    [Fact]
    public async Task Handler_NoActiveHolds_ShouldReturnEmptyListWithZeroRemainingSeconds()
    {
        var flight = new Flight { Id = 100 };
        var uow = new FakeUnitOfWork(new FakeFlightRepository(flight), new FakeSeatHoldRepository(Array.Empty<SeatHold>()));
        var handler = new GetMySeatHoldsQueryHandler(uow, new FakeClock(FixedUtcNow));

        var response = await handler.Handle(new GetMySeatHoldsQuery(100, TestUserId), CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Empty(response.Data.HeldSeats);
        Assert.Equal(0, response.Data.RemainingSeconds);
    }

    [Fact]
    public async Task Handler_ActiveHolds_ShouldReturnHeldSeatsWithServerComputedRemainingSeconds()
    {
        var flight = new Flight { Id = 100 };
        var expires = FixedUtcNow.UtcDateTime.AddSeconds(450);
        var holds = new List<SeatHold>
        {
            new()
            {
                Id = 1,
                FlightId = 100,
                SeatId = 11,
                UserId = TestUserId,
                ExpiresAtUtc = expires,
                Seat = new Seat { Id = 11, SeatNumber = "3A" }
            },
            new()
            {
                Id = 2,
                FlightId = 100,
                SeatId = 12,
                UserId = TestUserId,
                ExpiresAtUtc = expires,
                Seat = new Seat { Id = 12, SeatNumber = "3B" }
            },
            new()
            {
                // Another user's hold - should not be returned for TestUserId
                Id = 3,
                FlightId = 100,
                SeatId = 20,
                UserId = OtherUserId,
                ExpiresAtUtc = expires,
                Seat = new Seat { Id = 20, SeatNumber = "5C" }
            }
        };

        var uow = new FakeUnitOfWork(new FakeFlightRepository(flight), new FakeSeatHoldRepository(holds));
        var handler = new GetMySeatHoldsQueryHandler(uow, new FakeClock(FixedUtcNow));

        var response = await handler.Handle(new GetMySeatHoldsQuery(100, TestUserId), CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(2, response.Data.HeldSeats.Count);
        Assert.Equal(450, response.Data.RemainingSeconds);
        Assert.Equal("3A", response.Data.HeldSeats[0].SeatNumber);
        Assert.Equal("3B", response.Data.HeldSeats[1].SeatNumber);
    }
}
