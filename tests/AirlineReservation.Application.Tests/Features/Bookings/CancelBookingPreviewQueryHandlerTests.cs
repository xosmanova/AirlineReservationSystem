using System.Linq.Expressions;
using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.Common.Services;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.Features.Bookings.Queries.CancelBookingPreview;
using AirlineReservation.Domain.Common;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using Xunit;

namespace AirlineReservation.Application.Tests.Features.Bookings;

public class CancelBookingPreviewQueryHandlerTests
{
    private static readonly Guid OwnerUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private readonly TimeProvider _timeProvider;
    private readonly ICancellationPolicyService _policyService;

    public CancelBookingPreviewQueryHandlerTests()
    {
        _timeProvider = new FakeClock(new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero));
        _policyService = new CancellationPolicyService();
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static Booking CreateTestBooking(
        int bookingId,
        Guid userId,
        BookingStatus status,
        decimal totalAmount,
        DateTime departureTimeUtc)
    {
        var flight = new Flight
        {
            Id = 1,
            FlightNumber = "J2-101",
            DepartureTimeUtc = departureTimeUtc,
            ArrivalTimeUtc = departureTimeUtc.AddHours(3),
            Status = FlightStatus.Scheduled
        };

        var ticket = new Ticket
        {
            Id = 10,
            TicketNumber = "ET-TEST-001",
            FlightId = flight.Id,
            Flight = flight,
            SeatId = 1,
            PassengerId = 1,
            Price = totalAmount,
            IsCancelled = status == BookingStatus.Cancelled
        };

        return new Booking
        {
            Id = bookingId,
            PnrCode = "TEST01",
            UserId = userId,
            TotalAmount = totalAmount,
            Status = status,
            ContactEmail = "user@test.com",
            ContactPhone = "+994501234567",
            CreatedAt = DateTime.UtcNow,
            Tickets = [ticket]
        };
    }

    [Fact]
    public async Task Handle_DepartureOver24Hours_Returns100PercentRefundPreview()
    {
        // 48 hours away
        var departureTime = _timeProvider.GetUtcNow().UtcDateTime.AddHours(48);
        var booking = CreateTestBooking(1, OwnerUserId, BookingStatus.Confirmed, 200.00m, departureTime);

        var uow = new FakeUnitOfWork(new FakeBookingRepository(booking));
        var handler = new CancelBookingPreviewQueryHandler(uow, _policyService, _timeProvider);

        var query = new CancelBookingPreviewQuery(1, OwnerUserId, IsAdmin: false);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.CanCancel);
        Assert.Equal(100.00m, result.Data.RefundPercentage);
        Assert.Equal(200.00m, result.Data.RefundAmount);
        Assert.Equal(0.00m, result.Data.PenaltyAmount);
        Assert.Equal("TEST01", result.Data.PnrCode);
    }

    [Fact]
    public async Task Handle_DepartureWithin24Hours_Returns50PercentRefundPreview()
    {
        // 12 hours away
        var departureTime = _timeProvider.GetUtcNow().UtcDateTime.AddHours(12);
        var booking = CreateTestBooking(2, OwnerUserId, BookingStatus.Confirmed, 250.00m, departureTime);

        var uow = new FakeUnitOfWork(new FakeBookingRepository(booking));
        var handler = new CancelBookingPreviewQueryHandler(uow, _policyService, _timeProvider);

        var query = new CancelBookingPreviewQuery(2, OwnerUserId, IsAdmin: false);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.Data!.CanCancel);
        Assert.Equal(50.00m, result.Data.RefundPercentage);
        Assert.Equal(125.00m, result.Data.RefundAmount);
        Assert.Equal(125.00m, result.Data.PenaltyAmount);
    }

    [Fact]
    public async Task Handle_AdminViewingAnotherUserBooking_Succeeds()
    {
        var departureTime = _timeProvider.GetUtcNow().UtcDateTime.AddHours(30);
        var booking = CreateTestBooking(3, OwnerUserId, BookingStatus.Confirmed, 100.00m, departureTime);

        var uow = new FakeUnitOfWork(new FakeBookingRepository(booking));
        var handler = new CancelBookingPreviewQueryHandler(uow, _policyService, _timeProvider);

        // Request made by OtherUserId with IsAdmin = true
        var query = new CancelBookingPreviewQuery(3, OtherUserId, IsAdmin: true);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(3, result.Data!.BookingId);
    }

    [Fact]
    public async Task Handle_NonOwnerNonAdmin_ThrowsForbiddenException()
    {
        var departureTime = _timeProvider.GetUtcNow().UtcDateTime.AddHours(30);
        var booking = CreateTestBooking(4, OwnerUserId, BookingStatus.Confirmed, 100.00m, departureTime);

        var uow = new FakeUnitOfWork(new FakeBookingRepository(booking));
        var handler = new CancelBookingPreviewQueryHandler(uow, _policyService, _timeProvider);

        // Non-owner, non-admin
        var query = new CancelBookingPreviewQuery(4, OtherUserId, IsAdmin: false);

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_BookingNotFound_ThrowsNotFoundException()
    {
        var uow = new FakeUnitOfWork(new FakeBookingRepository(null));
        var handler = new CancelBookingPreviewQueryHandler(uow, _policyService, _timeProvider);

        var query = new CancelBookingPreviewQuery(999, OwnerUserId, IsAdmin: false);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_BookingAlreadyCancelled_ThrowsBadRequestException()
    {
        var departureTime = _timeProvider.GetUtcNow().UtcDateTime.AddHours(30);
        var booking = CreateTestBooking(5, OwnerUserId, BookingStatus.Cancelled, 100.00m, departureTime);

        var uow = new FakeUnitOfWork(new FakeBookingRepository(booking));
        var handler = new CancelBookingPreviewQueryHandler(uow, _policyService, _timeProvider);

        var query = new CancelBookingPreviewQuery(5, OwnerUserId, IsAdmin: false);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(query, CancellationToken.None));
        Assert.Contains("already cancelled", ex.Message);
    }

    [Fact]
    public async Task Handle_FlightDeparted_ReturnsCanCancelFalse()
    {
        // 2 hours in the past
        var departureTime = _timeProvider.GetUtcNow().UtcDateTime.AddHours(-2);
        var booking = CreateTestBooking(6, OwnerUserId, BookingStatus.Confirmed, 100.00m, departureTime);

        var uow = new FakeUnitOfWork(new FakeBookingRepository(booking));
        var handler = new CancelBookingPreviewQueryHandler(uow, _policyService, _timeProvider);

        var query = new CancelBookingPreviewQuery(6, OwnerUserId, IsAdmin: false);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.False(result.Data!.CanCancel);
        Assert.Equal(0.00m, result.Data.RefundAmount);
    }

    private sealed class FakeUnitOfWork(IBookingRepository bookingRepo) : IUnitOfWork
    {
        public IBookingRepository Bookings { get; } = bookingRepo;
        public IFlightRepository Flights => throw new NotImplementedException();
        public ISeatRepository Seats => throw new NotImplementedException();
        public IAirportRepository Airports => throw new NotImplementedException();
        public IAirlineRepository Airlines => throw new NotImplementedException();
        public ISeatHoldRepository SeatHolds => throw new NotImplementedException();
        public IGenericRepository<T> Repository<T>() where T : BaseEntity => throw new NotImplementedException();
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeBookingRepository(Booking? booking) : IBookingRepository
    {
        public Task<Booking?> GetByIdWithDetailsAsync(int id, bool trackChanges = true, CancellationToken ct = default)
        {
            if (booking != null && booking.Id == id)
            {
                return Task.FromResult<Booking?>(booking);
            }
            return Task.FromResult<Booking?>(null);
        }

        public Task<Booking?> GetByPnrWithDetailsAsync(string pnrCode, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> PnrExistsAsync(string pnrCode, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Booking>> GetBookingsByUserIdAsync(Guid userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Booking>> GetAllWithDetailsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Booking?> GetByIdAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Booking>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Booking> AddAsync(Booking entity, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(Booking entity, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteAsync(Booking entity, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Booking>> FindAsync(Expression<Func<Booking, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Booking?> FirstOrDefaultAsync(Expression<Func<Booking, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> AnyAsync(Expression<Func<Booking, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> CountAsync(Expression<Func<Booking, bool>>? predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddRangeAsync(IEnumerable<Booking> entities, CancellationToken ct = default) => throw new NotImplementedException();
        public void Update(Booking entity) { }
        public void Delete(Booking entity) { }
        public void DeleteRange(IEnumerable<Booking> entities) { }
    }
}
