using System.Linq.Expressions;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.Features.Bookings.Queries.GetMyBookings;
using AirlineReservation.Domain.Common;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using Xunit;

namespace AirlineReservation.Application.Tests.Features.Bookings;

public class GetMyBookingsQueryHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TestUserId = Guid.NewGuid();

    private sealed class FakeClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static Booking CreateBooking(int id, string pnr, BookingStatus status, DateTime flightDepartureUtc)
    {
        var flight = new Flight
        {
            Id = id * 10,
            FlightNumber = $"J2-{id}",
            DepartureTimeUtc = flightDepartureUtc,
            ArrivalTimeUtc = flightDepartureUtc.AddHours(3),
            Status = FlightStatus.Scheduled
        };

        var ticket = new Ticket
        {
            Id = id,
            TicketNumber = $"ET-{pnr}-1A",
            Flight = flight,
            FlightId = flight.Id,
            Seat = new Seat { SeatNumber = "1A", SeatClass = SeatClass.Business },
            Passenger = new Passenger { FirstName = "Ali", LastName = "Aliyev" },
            Price = 100m,
            IsCancelled = status == BookingStatus.Cancelled
        };

        return new Booking
        {
            Id = id,
            PnrCode = pnr,
            UserId = TestUserId,
            TotalAmount = 100m,
            Status = status,
            CreatedAt = FixedUtcNow.UtcDateTime.AddDays(-5),
            Tickets = [ticket]
        };
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

    private sealed class FakeBookingRepository(IReadOnlyList<Booking> bookings) : IBookingRepository
    {
        public Task<IReadOnlyList<Booking>> GetBookingsByUserIdAsync(Guid userId, CancellationToken ct = default)
        {
            var userBookings = bookings.Where(b => b.UserId == userId).ToList();
            return Task.FromResult<IReadOnlyList<Booking>>(userBookings);
        }

        public Task<Booking?> GetByPnrWithDetailsAsync(string pnrCode, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> PnrExistsAsync(string pnrCode, CancellationToken ct = default) => throw new NotImplementedException();
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

    [Fact]
    public async Task Handle_NoStatusFilter_ReturnsAllUserBookingsPaginated()
    {
        var now = FixedUtcNow.UtcDateTime;
        var bookings = new List<Booking>
        {
            CreateBooking(1, "PNR001", BookingStatus.Confirmed, now.AddDays(2)), // upcoming
            CreateBooking(2, "PNR002", BookingStatus.Confirmed, now.AddDays(-2)), // past
            CreateBooking(3, "PNR003", BookingStatus.Cancelled, now.AddDays(1))  // cancelled
        };

        var uow = new FakeUnitOfWork(new FakeBookingRepository(bookings));
        var clock = new FakeClock(FixedUtcNow);
        var handler = new GetMyBookingsQueryHandler(uow, clock);

        var query = new GetMyBookingsQuery(TestUserId, Status: null, PageNumber: 1, PageSize: 10);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(3, result.Data!.TotalCount);
        Assert.Equal(3, result.Data.Items.Count);
    }

    [Fact]
    public async Task Handle_FilterUpcoming_ReturnsOnlyFutureActiveBookings()
    {
        var now = FixedUtcNow.UtcDateTime;
        var bookings = new List<Booking>
        {
            CreateBooking(1, "PNR001", BookingStatus.Confirmed, now.AddDays(2)), // upcoming
            CreateBooking(2, "PNR002", BookingStatus.Confirmed, now.AddDays(-2)), // past
            CreateBooking(3, "PNR003", BookingStatus.Cancelled, now.AddDays(3))  // cancelled (even if future)
        };

        var uow = new FakeUnitOfWork(new FakeBookingRepository(bookings));
        var clock = new FakeClock(FixedUtcNow);
        var handler = new GetMyBookingsQueryHandler(uow, clock);

        var query = new GetMyBookingsQuery(TestUserId, Status: "upcoming");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result?.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal("PNR001", result.Data.Items[0].PnrCode);
    }

    [Fact]
    public async Task Handle_FilterPast_ReturnsOnlyPastActiveBookings()
    {
        var now = FixedUtcNow.UtcDateTime;
        var bookings = new List<Booking>
        {
            CreateBooking(1, "PNR001", BookingStatus.Confirmed, now.AddDays(2)), // upcoming
            CreateBooking(2, "PNR002", BookingStatus.Confirmed, now.AddDays(-2)), // past
            CreateBooking(3, "PNR003", BookingStatus.Cancelled, now.AddDays(-3))  // cancelled past
        };

        var uow = new FakeUnitOfWork(new FakeBookingRepository(bookings));
        var clock = new FakeClock(FixedUtcNow);
        var handler = new GetMyBookingsQueryHandler(uow, clock);

        var query = new GetMyBookingsQuery(TestUserId, Status: "past");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result?.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal("PNR002", result.Data.Items[0].PnrCode);
    }

    [Fact]
    public async Task Handle_FilterCancelled_ReturnsOnlyCancelledBookings()
    {
        var now = FixedUtcNow.UtcDateTime;
        var bookings = new List<Booking>
        {
            CreateBooking(1, "PNR001", BookingStatus.Confirmed, now.AddDays(2)),
            CreateBooking(2, "PNR002", BookingStatus.Cancelled, now.AddDays(1)),
            CreateBooking(3, "PNR003", BookingStatus.Cancelled, now.AddDays(-1))
        };

        var uow = new FakeUnitOfWork(new FakeBookingRepository(bookings));
        var clock = new FakeClock(FixedUtcNow);
        var handler = new GetMyBookingsQueryHandler(uow, clock);

        var query = new GetMyBookingsQuery(TestUserId, Status: "cancelled");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result?.Data);
        Assert.Equal(2, result.Data.TotalCount);
        Assert.Equal(2, result.Data.Items.Count);
        Assert.All(result.Data.Items, item => Assert.Equal("Cancelled", item.Status));
    }

    [Fact]
    public async Task Handle_Pagination_CorrectlyPagesData()
    {
        var now = FixedUtcNow.UtcDateTime;
        var bookings = Enumerable.Range(1, 5)
            .Select(i => CreateBooking(i, $"PNR00{i}", BookingStatus.Confirmed, now.AddDays(i)))
            .ToList();

        var uow = new FakeUnitOfWork(new FakeBookingRepository(bookings));
        var clock = new FakeClock(FixedUtcNow);
        var handler = new GetMyBookingsQueryHandler(uow, clock);

        // Page 2 with size 2
        var query = new GetMyBookingsQuery(TestUserId, Status: null, PageNumber: 2, PageSize: 2);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result?.Data);
        Assert.Equal(5, result.Data.TotalCount);
        Assert.Equal(2, result.Data.Items.Count);
        Assert.Equal(3, result.Data.TotalPages);
        Assert.True(result.Data.HasPreviousPage);
        Assert.True(result.Data.HasNextPage);
        Assert.Equal("PNR003", result.Data.Items[0].PnrCode);
        Assert.Equal("PNR004", result.Data.Items[1].PnrCode);
    }
}
