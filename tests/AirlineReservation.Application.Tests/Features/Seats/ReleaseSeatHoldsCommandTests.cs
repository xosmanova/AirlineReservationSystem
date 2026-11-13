using System.Linq.Expressions;
using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Flight;
using AirlineReservation.Application.Features.Seats.Commands.ReleaseSeatHolds;
using AirlineReservation.Domain.Common;
using AirlineReservation.Domain.Entities;
using FluentValidation.TestHelper;
using Xunit;

namespace AirlineReservation.Application.Tests.Features.Seats;

public class ReleaseSeatHoldsCommandTests
{
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

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

    private sealed class FakeSeatHoldRepository(List<SeatHold> holds) : ISeatHoldRepository
    {
        public List<SeatHold> Holds { get; } = holds;

        public Task<int> ReleaseUserHoldsForFlightAsync(int flightId, Guid userId, CancellationToken ct = default)
        {
            int removed = Holds.RemoveAll(h => h.FlightId == flightId && h.UserId == userId);
            return Task.FromResult(removed);
        }

        public Task<IReadOnlyList<SeatHold>> GetActiveHoldsByFlightIdAsync(int flightId, DateTime utcNow, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SeatHold>> GetUserActiveHoldsForFlightAsync(int flightId, Guid userId, DateTime utcNow, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SeatHold>> GetUserActiveHoldsAsync(Guid userId, DateTime utcNow, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlySet<int>> GetActiveHeldSeatIdsForFlightAsync(int flightId, DateTime utcNow, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> ReleaseOtherFlightHoldsForUserAsync(int currentFlightId, Guid userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> DeleteExpiredHoldsForSeatsAsync(int flightId, IReadOnlyCollection<int> seatIds, DateTime utcNow, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> DeleteAllExpiredHoldsAsync(DateTime utcNow, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddRangeAsync(IEnumerable<SeatHold> holds, CancellationToken ct = default) => throw new NotImplementedException();
    }

    #endregion

    [Fact]
    public void Validator_ValidCommand_ShouldPass()
    {
        var validator = new ReleaseSeatHoldsCommandValidator();
        var result = validator.TestValidate(new ReleaseSeatHoldsCommand(100, TestUserId));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validator_InvalidFlightId_ShouldFail(int flightId)
    {
        var validator = new ReleaseSeatHoldsCommandValidator();
        var result = validator.TestValidate(new ReleaseSeatHoldsCommand(flightId, TestUserId));
        result.ShouldHaveValidationErrorFor(x => x.FlightId);
    }

    [Fact]
    public void Validator_EmptyUserId_ShouldFail()
    {
        var validator = new ReleaseSeatHoldsCommandValidator();
        var result = validator.TestValidate(new ReleaseSeatHoldsCommand(100, Guid.Empty));
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public async Task Handler_FlightNotFound_ShouldThrowNotFoundException()
    {
        var uow = new FakeUnitOfWork(new FakeFlightRepository(null), new FakeSeatHoldRepository(new List<SeatHold>()));
        var handler = new ReleaseSeatHoldsCommandHandler(uow);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new ReleaseSeatHoldsCommand(100, TestUserId), CancellationToken.None));
    }

    [Fact]
    public async Task Handler_UserHasHolds_ShouldOnlyReleaseOwnHoldsAndReturnSuccess()
    {
        var flight = new Flight { Id = 100 };
        var holds = new List<SeatHold>
        {
            new() { Id = 1, FlightId = 100, SeatId = 10, UserId = TestUserId },
            new() { Id = 2, FlightId = 100, SeatId = 11, UserId = TestUserId },
            new() { Id = 3, FlightId = 100, SeatId = 12, UserId = OtherUserId } // Someone else's hold
        };
        var holdRepo = new FakeSeatHoldRepository(holds);
        var uow = new FakeUnitOfWork(new FakeFlightRepository(flight), holdRepo);
        var handler = new ReleaseSeatHoldsCommandHandler(uow);

        var result = await handler.Handle(new ReleaseSeatHoldsCommand(100, TestUserId), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("2 seat hold(s) released", result.Message);
        Assert.Single(holdRepo.Holds);
        Assert.Equal(OtherUserId, holdRepo.Holds[0].UserId); // Other user's hold untouched!
    }

    [Fact]
    public async Task Handler_UserHasNoHolds_ShouldReturnSuccessIdempotently()
    {
        var flight = new Flight { Id = 100 };
        var holds = new List<SeatHold>
        {
            new() { Id = 3, FlightId = 100, SeatId = 12, UserId = OtherUserId }
        };
        var holdRepo = new FakeSeatHoldRepository(holds);
        var uow = new FakeUnitOfWork(new FakeFlightRepository(flight), holdRepo);
        var handler = new ReleaseSeatHoldsCommandHandler(uow);

        var result = await handler.Handle(new ReleaseSeatHoldsCommand(100, TestUserId), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("No active seat holds were found to release", result.Message);
        Assert.Single(holdRepo.Holds); // Other user's hold still untouched!
    }
}
