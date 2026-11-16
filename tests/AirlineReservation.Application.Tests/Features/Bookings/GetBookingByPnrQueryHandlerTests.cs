using System.Linq.Expressions;
using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.Features.Bookings.Queries.GetBookingByPnr;
using AirlineReservation.Domain.Common;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using Xunit;

namespace AirlineReservation.Application.Tests.Features.Bookings;

public class GetBookingByPnrQueryHandlerTests
{
    private static readonly Guid TestUserId = Guid.NewGuid();

    private static Booking CreateSampleBooking(string pnr = "AZ78KD", string lastName = "Aliyev")
    {
        var flight = new Flight
        {
            Id = 10,
            FlightNumber = "J2-100",
            DepartureTimeUtc = DateTime.UtcNow.AddDays(2),
            ArrivalTimeUtc = DateTime.UtcNow.AddDays(2).AddHours(3),
            Status = FlightStatus.Scheduled,
            Airline = new Airline { Name = "AZAL" },
            DepartureAirport = new Airport { IataCode = "GYD", Name = "Heydar Aliyev Int.", City = "Baku" },
            ArrivalAirport = new Airport { IataCode = "IST", Name = "Istanbul Airport", City = "Istanbul" }
        };

        var passenger = new Passenger
        {
            Id = 1,
            FirstName = "Ali",
            LastName = lastName,
            PassportNumber = "C12345678",
            DateOfBirth = new DateTime(1995, 5, 20)
        };

        var seat = new Seat
        {
            Id = 101,
            SeatNumber = "12A",
            SeatClass = SeatClass.Economy
        };

        var ticket = new Ticket
        {
            Id = 1,
            TicketNumber = $"ET-{pnr}-12A",
            Flight = flight,
            FlightId = flight.Id,
            Passenger = passenger,
            PassengerId = passenger.Id,
            Seat = seat,
            SeatId = seat.Id,
            Price = 120.00m,
            IsCancelled = false
        };

        var payment = new Payment
        {
            Id = 1,
            Amount = 120.00m,
            PaymentDate = DateTime.UtcNow,
            PaymentMethod = "CreditCard",
            TransactionReference = "TXN-1234567890",
            Status = PaymentStatus.Success
        };

        return new Booking
        {
            Id = 1,
            PnrCode = pnr,
            UserId = TestUserId,
            TotalAmount = 120.00m,
            Status = BookingStatus.Confirmed,
            ContactEmail = "passenger@example.com",
            ContactPhone = "+994501234567",
            CreatedAt = DateTime.UtcNow,
            Tickets = [ticket],
            Payment = payment
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

    private sealed class FakeBookingRepository(Booking? booking) : IBookingRepository
    {
        public Task<Booking?> GetByPnrWithDetailsAsync(string pnrCode, CancellationToken ct = default)
        {
            if (booking != null && string.Equals(booking.PnrCode, pnrCode, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<Booking?>(booking);
            }
            return Task.FromResult<Booking?>(null);
        }

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

    [Fact]
    public async Task Handle_ValidPnrAndMatchingLastName_ReturnsBookingDetails()
    {
        var booking = CreateSampleBooking("AZ78KD", "Aliyev");
        var uow = new FakeUnitOfWork(new FakeBookingRepository(booking));
        var handler = new GetBookingByPnrQueryHandler(uow);

        var query = new GetBookingByPnrQuery("AZ78KD", "Aliyev");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("AZ78KD", result.Data.PnrCode);
        Assert.Equal("Confirmed", result.Data.Status);
        Assert.Equal("J2-100", result.Data.FlightNumber);
        Assert.Equal("GYD", result.Data.DepartureAirportCode);
        Assert.Equal("IST", result.Data.ArrivalAirportCode);
        Assert.Single(result.Data.Tickets);
        Assert.Equal("Aliyev", result.Data.Tickets[0].PassengerLastName);
        Assert.NotNull(result.Data.Payment);
        Assert.Equal(120.00m, result.Data.Payment.Amount);
    }

    [Fact]
    public async Task Handle_CaseInsensitivePnrAndLastName_MatchesSuccessfully()
    {
        var booking = CreateSampleBooking("AZ78KD", "Aliyev");
        var uow = new FakeUnitOfWork(new FakeBookingRepository(booking));
        var handler = new GetBookingByPnrQueryHandler(uow);

        // Lowercase query values
        var query = new GetBookingByPnrQuery("az78kd", "aliyev");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("AZ78KD", result.Data!.PnrCode);
    }

    [Fact]
    public async Task Handle_PnrNotFound_ThrowsNotFoundException()
    {
        var uow = new FakeUnitOfWork(new FakeBookingRepository(null));
        var handler = new GetBookingByPnrQueryHandler(uow);

        var query = new GetBookingByPnrQuery("NONEXS", "Aliyev");

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PnrFound_ButLastNameDoesNotMatch_ThrowsNotFoundException()
    {
        var booking = CreateSampleBooking("AZ78KD", "Aliyev");
        var uow = new FakeUnitOfWork(new FakeBookingRepository(booking));
        var handler = new GetBookingByPnrQueryHandler(uow);

        // Wrong last name "Mammadov" instead of "Aliyev"
        var query = new GetBookingByPnrQuery("AZ78KD", "Mammadov");

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(query, CancellationToken.None));
        Assert.Contains("No passenger with last name 'Mammadov'", ex.Message);
    }
}
