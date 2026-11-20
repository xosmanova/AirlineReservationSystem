using System.Linq.Expressions;
using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.Common.Services;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.Features.Bookings.Commands.CancelBooking;
using AirlineReservation.Domain.Common;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using Xunit;

namespace AirlineReservation.Application.Tests.Features.Bookings;

public class CancelBookingCommandHandlerTests
{
    private static readonly Guid OwnerUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private readonly TimeProvider _timeProvider;
    private readonly ICancellationPolicyService _policyService;

    public CancelBookingCommandHandlerTests()
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

        var payment = new Payment
        {
            Id = 100,
            BookingId = bookingId,
            Amount = totalAmount,
            PaymentDate = DateTime.UtcNow,
            PaymentMethod = "MockGateway",
            TransactionReference = "TX-123456",
            Status = PaymentStatus.Success
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
            Tickets = [ticket],
            Payment = payment
        };
    }

    [Fact]
    public async Task Handle_DepartureOver24Hours_CancelsBookingWithFullRefund_AndUpdatesTicketsAndPayment()
    {
        var departureTime = _timeProvider.GetUtcNow().UtcDateTime.AddHours(48);
        var booking = CreateTestBooking(1, OwnerUserId, BookingStatus.Confirmed, 200.00m, departureTime);

        var uow = new FakeTransactionalUnitOfWork(new FakeBookingRepository(booking));
        var handler = new CancelBookingCommandHandler(uow, _policyService, _timeProvider);

        var command = new CancelBookingCommand(1, OwnerUserId, IsAdmin: false);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Cancelled", result.Data!.Status);
        Assert.Equal(200.00m, result.Data.RefundAmount);
        Assert.NotNull(result.Data.CancelledAtUtc);

        // Verify entities were updated in place
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(200.00m, booking.RefundAmount);
        Assert.True(booking.Tickets.First().IsCancelled);
        Assert.NotNull(booking.Tickets.First().CancelledAtUtc);
        Assert.Equal(PaymentStatus.Refunded, booking.Payment!.Status);

        // Verify transaction commits
        Assert.True(uow.TransactionCommitted);
        Assert.False(uow.TransactionRolledBack);
        Assert.True(uow.ChangesSaved);
    }

    [Fact]
    public async Task Handle_DepartureWithin24Hours_CancelsBookingWith50PercentRefund()
    {
        var departureTime = _timeProvider.GetUtcNow().UtcDateTime.AddHours(10);
        var booking = CreateTestBooking(2, OwnerUserId, BookingStatus.Confirmed, 300.00m, departureTime);

        var uow = new FakeTransactionalUnitOfWork(new FakeBookingRepository(booking));
        var handler = new CancelBookingCommandHandler(uow, _policyService, _timeProvider);

        var command = new CancelBookingCommand(2, OwnerUserId, IsAdmin: false);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(150.00m, result.Data!.RefundAmount);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(150.00m, booking.RefundAmount);
        Assert.True(booking.Tickets.First().IsCancelled);
        Assert.Equal(PaymentStatus.Refunded, booking.Payment!.Status);
        Assert.True(uow.TransactionCommitted);
    }

    [Fact]
    public async Task Handle_AdminCancellingAnotherUserBooking_Succeeds()
    {
        var departureTime = _timeProvider.GetUtcNow().UtcDateTime.AddHours(30);
        var booking = CreateTestBooking(3, OwnerUserId, BookingStatus.Confirmed, 120.00m, departureTime);

        var uow = new FakeTransactionalUnitOfWork(new FakeBookingRepository(booking));
        var handler = new CancelBookingCommandHandler(uow, _policyService, _timeProvider);

        var command = new CancelBookingCommand(3, OtherUserId, IsAdmin: true);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.True(uow.TransactionCommitted);
    }

    [Fact]
    public async Task Handle_NonOwnerNonAdmin_ThrowsForbiddenException()
    {
        var departureTime = _timeProvider.GetUtcNow().UtcDateTime.AddHours(30);
        var booking = CreateTestBooking(4, OwnerUserId, BookingStatus.Confirmed, 120.00m, departureTime);

        var uow = new FakeTransactionalUnitOfWork(new FakeBookingRepository(booking));
        var handler = new CancelBookingCommandHandler(uow, _policyService, _timeProvider);

        var command = new CancelBookingCommand(4, OtherUserId, IsAdmin: false);

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(command, CancellationToken.None));
        Assert.False(uow.TransactionCommitted);
    }

    [Fact]
    public async Task Handle_BookingNotFound_ThrowsNotFoundException()
    {
        var uow = new FakeTransactionalUnitOfWork(new FakeBookingRepository(null));
        var handler = new CancelBookingCommandHandler(uow, _policyService, _timeProvider);

        var command = new CancelBookingCommand(999, OwnerUserId, IsAdmin: false);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_BookingAlreadyCancelled_ThrowsConflictException()
    {
        var departureTime = _timeProvider.GetUtcNow().UtcDateTime.AddHours(30);
        var booking = CreateTestBooking(5, OwnerUserId, BookingStatus.Cancelled, 120.00m, departureTime);

        var uow = new FakeTransactionalUnitOfWork(new FakeBookingRepository(booking));
        var handler = new CancelBookingCommandHandler(uow, _policyService, _timeProvider);

        var command = new CancelBookingCommand(5, OwnerUserId, IsAdmin: false);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("already cancelled", ex.Message);
    }

    [Fact]
    public async Task Handle_FlightAlreadyDeparted_ThrowsBadRequestException()
    {
        // 1 hour ago
        var departureTime = _timeProvider.GetUtcNow().UtcDateTime.AddHours(-1);
        var booking = CreateTestBooking(6, OwnerUserId, BookingStatus.Confirmed, 120.00m, departureTime);

        var uow = new FakeTransactionalUnitOfWork(new FakeBookingRepository(booking));
        var handler = new CancelBookingCommandHandler(uow, _policyService, _timeProvider);

        var command = new CancelBookingCommand(6, OwnerUserId, IsAdmin: false);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("departed", ex.Message);
        Assert.False(uow.TransactionCommitted);
    }

    [Fact]
    public async Task Handle_DatabaseError_RollsBackTransactionAndThrows()
    {
        var departureTime = _timeProvider.GetUtcNow().UtcDateTime.AddHours(48);
        var booking = CreateTestBooking(7, OwnerUserId, BookingStatus.Confirmed, 100.00m, departureTime);

        var uow = new FakeTransactionalUnitOfWork(new FakeBookingRepository(booking))
        {
            ThrowOnSaveChanges = true
        };
        var handler = new CancelBookingCommandHandler(uow, _policyService, _timeProvider);

        var command = new CancelBookingCommand(7, OwnerUserId, IsAdmin: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
        Assert.True(uow.TransactionRolledBack);
        Assert.False(uow.TransactionCommitted);
    }

    private sealed class FakeTransactionalUnitOfWork(IBookingRepository bookingRepo) : IUnitOfWork
    {
        public IBookingRepository Bookings { get; } = bookingRepo;
        public IFlightRepository Flights => throw new NotImplementedException();
        public ISeatRepository Seats => throw new NotImplementedException();
        public IAirportRepository Airports => throw new NotImplementedException();
        public IAirlineRepository Airlines => throw new NotImplementedException();
        public ISeatHoldRepository SeatHolds => throw new NotImplementedException();
        public IGenericRepository<T> Repository<T>() where T : BaseEntity => throw new NotImplementedException();

        public bool ChangesSaved { get; private set; }
        public bool TransactionCommitted { get; private set; }
        public bool TransactionRolledBack { get; private set; }
        public bool ThrowOnSaveChanges { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            if (ThrowOnSaveChanges)
            {
                throw new InvalidOperationException("Simulated DB write failure.");
            }
            ChangesSaved = true;
            return Task.FromResult(1);
        }

        public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task CommitTransactionAsync(CancellationToken ct = default)
        {
            TransactionCommitted = true;
            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken ct = default)
        {
            TransactionRolledBack = true;
            return Task.CompletedTask;
        }

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
