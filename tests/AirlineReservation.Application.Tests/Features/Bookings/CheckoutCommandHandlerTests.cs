using System.Linq.Expressions;
using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Generators;
using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Common.Services;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Booking;
using AirlineReservation.Application.DTOs.Flight;
using AirlineReservation.Application.Features.Bookings.Commands.Checkout;
using AirlineReservation.Domain.Common;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using Xunit;

namespace AirlineReservation.Application.Tests.Features.Bookings;

public class CheckoutCommandHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    private sealed class FakeClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static FlightSeatMapData CreateSeatMapData(
        int flightId = 100,
        FlightStatus status = FlightStatus.Scheduled,
        int hoursUntilDeparture = 48,
        decimal basePrice = 100.00m,
        IReadOnlySet<int>? occupiedSeatIds = null)
    {
        var seats = new List<SeatMapSeatItem>
        {
            new(101, 1, "1A", 1, "A", SeatClass.Business, 2.0m, true, false, false),
            new(102, 1, "1B", 1, "B", SeatClass.Business, 2.0m, false, false, false),
            new(103, 1, "1C", 1, "C", SeatClass.Business, 2.0m, false, true, false),
            new(104, 1, "12A", 12, "A", SeatClass.Economy, 1.0m, true, false, false),
            new(105, 1, "12B", 12, "B", SeatClass.Economy, 1.0m, false, false, false)
        };

        return new FlightSeatMapData(
            FlightId: flightId,
            FlightNumber: "J2100",
            AirlineName: "AZAL",
            AircraftId: 1,
            AircraftModel: "Airbus A320",
            BasePrice: basePrice,
            DepartureTimeUtc: FixedUtcNow.UtcDateTime.AddHours(hoursUntilDeparture),
            Status: status,
            Seats: seats,
            OccupiedSeatIds: occupiedSeatIds ?? new HashSet<int>());
    }

    private static CheckoutCommand CreateValidCommand(int flightId = 100, List<int>? seatIds = null)
    {
        var chosenSeats = seatIds ?? [104, 105];
        return new CheckoutCommand
        {
            FlightId = flightId,
            UserId = TestUserId,
            Passengers = chosenSeats.Select((sid, i) => new CheckoutPassengerDto
            {
                FirstName = $"Passenger{i + 1}",
                LastName = "Aliyev",
                PassportNumber = $"C{i + 1:D8}",
                DateOfBirth = new DateTime(1995, 5, 20),
                SeatId = sid
            }).ToList(),
            Contact = new CheckoutContactDto
            {
                Email = "passenger@example.com",
                PhoneNumber = "+994501234567"
            },
            Card = new CardPaymentDto
            {
                CardNumber = "4111111111111111",
                CardHolderName = "PASSENGER ALIYEV",
                ExpiryMonthYear = "12/28",
                Cvv = "123"
            }
        };
    }

    #region Fakes

    private sealed class FakePaymentGateway : IPaymentGateway
    {
        public bool ShouldSucceed { get; set; } = true;
        public string? FailureMessage { get; set; }
        public PaymentProcessRequest? LastRequest { get; private set; }

        public Task<PaymentProcessResult> ProcessPaymentAsync(PaymentProcessRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (ShouldSucceed)
            {
                return Task.FromResult(new PaymentProcessResult(true, "TXN-123456", null));
            }

            return Task.FromResult(new PaymentProcessResult(false, null, FailureMessage ?? "Card declined"));
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public IFlightRepository Flights { get; }
        public ISeatRepository Seats { get; }
        public ISeatHoldRepository SeatHolds { get; }
        public IBookingRepository Bookings { get; }
        public IAirportRepository Airports => throw new NotImplementedException();
        public IAirlineRepository Airlines => throw new NotImplementedException();

        public bool TransactionStarted { get; private set; }
        public bool TransactionCommitted { get; private set; }
        public bool TransactionRolledBack { get; private set; }
        public bool ThrowUniqueConstraintOnCommit { get; set; }

        public FakeUnitOfWork(
            IFlightRepository flightRepo,
            ISeatRepository seatRepo,
            ISeatHoldRepository seatHoldRepo,
            IBookingRepository bookingRepo)
        {
            Flights = flightRepo;
            Seats = seatRepo;
            SeatHolds = seatHoldRepo;
            Bookings = bookingRepo;
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

        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);

        public IGenericRepository<T> Repository<T>() where T : BaseEntity => throw new NotImplementedException();

        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeFlightRepository(FlightSeatMapData? seatMapData) : IFlightRepository
    {
        public Task<FlightSeatMapData?> GetFlightSeatMapDataAsync(int flightId, CancellationToken ct = default)
            => Task.FromResult(seatMapData != null && seatMapData.FlightId == flightId ? seatMapData : null);

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
        public List<SeatHold> ActiveHolds { get; set; } = [];
        public bool HoldsReleased { get; private set; }

        public Task<IReadOnlyList<SeatHold>> GetUserActiveHoldsForFlightAsync(int flightId, Guid userId, DateTime utcNow, CancellationToken ct = default)
        {
            var userHolds = ActiveHolds
                .Where(h => h.FlightId == flightId && h.UserId == userId && h.ExpiresAtUtc > utcNow)
                .ToList();
            return Task.FromResult<IReadOnlyList<SeatHold>>(userHolds);
        }

        public Task<int> ReleaseUserHoldsForFlightAsync(int flightId, Guid userId, CancellationToken ct = default)
        {
            HoldsReleased = true;
            int removed = ActiveHolds.RemoveAll(h => h.FlightId == flightId && h.UserId == userId);
            return Task.FromResult(removed);
        }

        public Task<IReadOnlyList<SeatHold>> GetActiveHoldsByFlightIdAsync(int flightId, DateTime utcNow, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SeatHold>> GetUserActiveHoldsAsync(Guid userId, DateTime utcNow, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlySet<int>> GetActiveHeldSeatIdsForFlightAsync(int flightId, DateTime utcNow, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> ReleaseOtherFlightHoldsForUserAsync(int currentFlightId, Guid userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> DeleteExpiredHoldsForSeatsAsync(int flightId, IReadOnlyCollection<int> seatIds, DateTime utcNow, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> DeleteAllExpiredHoldsAsync(DateTime utcNow, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddRangeAsync(IEnumerable<SeatHold> holds, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeBookingRepository : IBookingRepository
    {
        public List<Booking> SavedBookings { get; } = [];

        public Task<bool> PnrExistsAsync(string pnrCode, CancellationToken ct = default)
        {
            return Task.FromResult(SavedBookings.Any(b => b.PnrCode == pnrCode));
        }

        public Task<Booking> AddAsync(Booking entity, CancellationToken ct = default)
        {
            entity.Id = SavedBookings.Count + 1;
            SavedBookings.Add(entity);
            return Task.FromResult(entity);
        }

        public Task<Booking?> GetByPnrWithDetailsAsync(string pnrCode, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Booking>> GetBookingsByUserIdAsync(Guid userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Booking>> GetAllWithDetailsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Booking?> GetByIdAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Booking>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Booking>> FindAsync(Expression<Func<Booking, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Booking?> FirstOrDefaultAsync(Expression<Func<Booking, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> AnyAsync(Expression<Func<Booking, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> CountAsync(Expression<Func<Booking, bool>>? predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddRangeAsync(IEnumerable<Booking> entities, CancellationToken ct = default) => throw new NotImplementedException();
        public void Update(Booking entity) { }
        public void Delete(Booking entity) { }
        public void DeleteRange(IEnumerable<Booking> entities) { }
    }

    #endregion

    [Fact]
    public async Task Handle_ValidRequestWithActiveHolds_CompletesBookingSuccessfully()
    {
        // Arrange
        var seatMap = CreateSeatMapData(100);
        var clock = new FakeClock(FixedUtcNow);
        var pricingService = new PricingService(new PricingSettings(), clock);
        var pnrGenerator = new PnrGenerator();
        var paymentGateway = new FakePaymentGateway();

        var flightRepo = new FakeFlightRepository(seatMap);
        var seatRepo = new FakeSeatRepository([]);
        var holdRepo = new FakeSeatHoldRepository
        {
            ActiveHolds =
            [
                new SeatHold { FlightId = 100, SeatId = 104, UserId = TestUserId, ExpiresAtUtc = FixedUtcNow.UtcDateTime.AddMinutes(5) },
                new SeatHold { FlightId = 100, SeatId = 105, UserId = TestUserId, ExpiresAtUtc = FixedUtcNow.UtcDateTime.AddMinutes(5) }
            ]
        };
        var bookingRepo = new FakeBookingRepository();
        var uow = new FakeUnitOfWork(flightRepo, seatRepo, holdRepo, bookingRepo);

        var handler = new CheckoutCommandHandler(uow, pricingService, pnrGenerator, paymentGateway, clock);
        var command = CreateValidCommand(100, [104, 105]);

        // Act
        var response = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(6, response.Data.PnrCode.Length);
        Assert.Equal("Confirmed", response.Data.Status);
        Assert.True(response.Data.TotalAmount > 0);

        Assert.True(uow.TransactionStarted);
        Assert.True(uow.TransactionCommitted);
        Assert.True(holdRepo.HoldsReleased);

        Assert.Single(bookingRepo.SavedBookings);
        var savedBooking = bookingRepo.SavedBookings[0];
        Assert.Equal(response.Data.PnrCode, savedBooking.PnrCode);
        Assert.Equal(TestUserId, savedBooking.UserId);
        Assert.Equal(BookingStatus.Confirmed, savedBooking.Status);
        Assert.Equal(2, savedBooking.Tickets.Count);
        Assert.NotNull(savedBooking.Payment);
        Assert.Equal(PaymentStatus.Success, savedBooking.Payment.Status);

        foreach (var ticket in savedBooking.Tickets)
        {
            Assert.StartsWith($"ET-{savedBooking.PnrCode}-", ticket.TicketNumber);
            Assert.False(ticket.IsCancelled);
            Assert.NotNull(ticket.Passenger);
        }
    }

    [Fact]
    public async Task Handle_FlightNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var clock = new FakeClock(FixedUtcNow);
        var pricingService = new PricingService(new PricingSettings(), clock);
        var uow = new FakeUnitOfWork(
            new FakeFlightRepository(null),
            new FakeSeatRepository([]),
            new FakeSeatHoldRepository(),
            new FakeBookingRepository());

        var handler = new CheckoutCommandHandler(uow, pricingService, new PnrGenerator(), new FakePaymentGateway(), clock);
        var command = CreateValidCommand(999);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_FlightNotSellable_Cancelled_ThrowsConflictException()
    {
        // Arrange
        var seatMap = CreateSeatMapData(100, status: FlightStatus.Cancelled);
        var clock = new FakeClock(FixedUtcNow);
        var pricingService = new PricingService(new PricingSettings(), clock);
        var uow = new FakeUnitOfWork(
            new FakeFlightRepository(seatMap),
            new FakeSeatRepository([]),
            new FakeSeatHoldRepository(),
            new FakeBookingRepository());

        var handler = new CheckoutCommandHandler(uow, pricingService, new PnrGenerator(), new FakePaymentGateway(), clock);
        var command = CreateValidCommand(100);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("not sellable", ex.Message);
    }

    [Fact]
    public async Task Handle_FlightNotSellable_DepartureInPast_ThrowsConflictException()
    {
        // Arrange
        var seatMap = CreateSeatMapData(100, hoursUntilDeparture: -2);
        var clock = new FakeClock(FixedUtcNow);
        var pricingService = new PricingService(new PricingSettings(), clock);
        var uow = new FakeUnitOfWork(
            new FakeFlightRepository(seatMap),
            new FakeSeatRepository([]),
            new FakeSeatHoldRepository(),
            new FakeBookingRepository());

        var handler = new CheckoutCommandHandler(uow, pricingService, new PnrGenerator(), new FakePaymentGateway(), clock);
        var command = CreateValidCommand(100);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("not sellable", ex.Message);
    }

    [Fact]
    public async Task Handle_SeatNotFoundAnywhere_ThrowsNotFoundException()
    {
        // Arrange
        var seatMap = CreateSeatMapData(100);
        var clock = new FakeClock(FixedUtcNow);
        var pricingService = new PricingService(new PricingSettings(), clock);
        var uow = new FakeUnitOfWork(
            new FakeFlightRepository(seatMap),
            new FakeSeatRepository([]), // Not found in DB either
            new FakeSeatHoldRepository(),
            new FakeBookingRepository());

        var handler = new CheckoutCommandHandler(uow, pricingService, new PnrGenerator(), new FakePaymentGateway(), clock);
        var command = CreateValidCommand(100, [9999]); // Seat 9999 does not exist

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SeatBelongsToDifferentAircraft_ThrowsBadRequestException()
    {
        // Arrange
        var seatMap = CreateSeatMapData(100);
        var clock = new FakeClock(FixedUtcNow);
        var pricingService = new PricingService(new PricingSettings(), clock);
        var otherSeat = new Seat { Id = 888, AircraftId = 2, SeatNumber = "30A" };
        var uow = new FakeUnitOfWork(
            new FakeFlightRepository(seatMap),
            new FakeSeatRepository([otherSeat]),
            new FakeSeatHoldRepository(),
            new FakeBookingRepository());

        var handler = new CheckoutCommandHandler(uow, pricingService, new PnrGenerator(), new FakePaymentGateway(), clock);
        var command = CreateValidCommand(100, [888]);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("do not belong to flight", ex.Message);
    }

    [Fact]
    public async Task Handle_SeatAlreadyOccupied_ThrowsConflictException()
    {
        // Arrange
        var seatMap = CreateSeatMapData(100, occupiedSeatIds: new HashSet<int> { 104 });
        var clock = new FakeClock(FixedUtcNow);
        var pricingService = new PricingService(new PricingSettings(), clock);
        var uow = new FakeUnitOfWork(
            new FakeFlightRepository(seatMap),
            new FakeSeatRepository([]),
            new FakeSeatHoldRepository(),
            new FakeBookingRepository());

        var handler = new CheckoutCommandHandler(uow, pricingService, new PnrGenerator(), new FakePaymentGateway(), clock);
        var command = CreateValidCommand(100, [104]);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("already occupied", ex.Message);
    }

    [Fact]
    public async Task Handle_UserLacksActiveHold_ThrowsConflictException()
    {
        // Arrange
        var seatMap = CreateSeatMapData(100);
        var clock = new FakeClock(FixedUtcNow);
        var pricingService = new PricingService(new PricingSettings(), clock);
        var holdRepo = new FakeSeatHoldRepository
        {
            // Holds held by ANOTHER user, not TestUserId
            ActiveHolds =
            [
                new SeatHold { FlightId = 100, SeatId = 104, UserId = OtherUserId, ExpiresAtUtc = FixedUtcNow.UtcDateTime.AddMinutes(5) }
            ]
        };
        var uow = new FakeUnitOfWork(
            new FakeFlightRepository(seatMap),
            new FakeSeatRepository([]),
            holdRepo,
            new FakeBookingRepository());

        var handler = new CheckoutCommandHandler(uow, pricingService, new PnrGenerator(), new FakePaymentGateway(), clock);
        var command = CreateValidCommand(100, [104]);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("Active seat hold required", ex.Message);
    }

    [Fact]
    public async Task Handle_UserHoldExpired_ThrowsConflictException()
    {
        // Arrange
        var seatMap = CreateSeatMapData(100);
        var clock = new FakeClock(FixedUtcNow);
        var pricingService = new PricingService(new PricingSettings(), clock);
        var holdRepo = new FakeSeatHoldRepository
        {
            // Hold expired 1 minute ago
            ActiveHolds =
            [
                new SeatHold { FlightId = 100, SeatId = 104, UserId = TestUserId, ExpiresAtUtc = FixedUtcNow.UtcDateTime.AddMinutes(-1) }
            ]
        };
        var uow = new FakeUnitOfWork(
            new FakeFlightRepository(seatMap),
            new FakeSeatRepository([]),
            holdRepo,
            new FakeBookingRepository());

        var handler = new CheckoutCommandHandler(uow, pricingService, new PnrGenerator(), new FakePaymentGateway(), clock);
        var command = CreateValidCommand(100, [104]);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("Active seat hold required", ex.Message);
    }

    [Fact]
    public async Task Handle_PaymentDeclined_ThrowsBadRequestException_AndRollsBack()
    {
        // Arrange
        var seatMap = CreateSeatMapData(100);
        var clock = new FakeClock(FixedUtcNow);
        var pricingService = new PricingService(new PricingSettings(), clock);
        var paymentGateway = new FakePaymentGateway { ShouldSucceed = false, FailureMessage = "Insufficient funds" };
        var holdRepo = new FakeSeatHoldRepository
        {
            ActiveHolds =
            [
                new SeatHold { FlightId = 100, SeatId = 104, UserId = TestUserId, ExpiresAtUtc = FixedUtcNow.UtcDateTime.AddMinutes(5) }
            ]
        };
        var uow = new FakeUnitOfWork(
            new FakeFlightRepository(seatMap),
            new FakeSeatRepository([]),
            holdRepo,
            new FakeBookingRepository());

        var handler = new CheckoutCommandHandler(uow, pricingService, new PnrGenerator(), paymentGateway, clock);
        var command = CreateValidCommand(100, [104]);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("Insufficient funds", ex.Message);
        Assert.False(uow.TransactionCommitted);
    }

    [Fact]
    public async Task Handle_UniqueConstraintCollisionOnCommit_ThrowsConflictException()
    {
        // Arrange
        var seatMap = CreateSeatMapData(100);
        var clock = new FakeClock(FixedUtcNow);
        var pricingService = new PricingService(new PricingSettings(), clock);
        var paymentGateway = new FakePaymentGateway();
        var holdRepo = new FakeSeatHoldRepository
        {
            ActiveHolds =
            [
                new SeatHold { FlightId = 100, SeatId = 104, UserId = TestUserId, ExpiresAtUtc = FixedUtcNow.UtcDateTime.AddMinutes(5) }
            ]
        };
        var uow = new FakeUnitOfWork(
            new FakeFlightRepository(seatMap),
            new FakeSeatRepository([]),
            holdRepo,
            new FakeBookingRepository())
        {
            ThrowUniqueConstraintOnCommit = true
        };

        var handler = new CheckoutCommandHandler(uow, pricingService, new PnrGenerator(), paymentGateway, clock);
        var command = CreateValidCommand(100, [104]);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("One or more selected seats were just booked by another transaction", ex.Message);
        Assert.True(uow.TransactionRolledBack);
    }
}
