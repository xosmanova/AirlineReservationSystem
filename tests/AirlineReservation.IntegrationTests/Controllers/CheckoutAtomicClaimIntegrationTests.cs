using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Common.Services;
using AirlineReservation.Application.Contracts.Identity;
using AirlineReservation.Application.DTOs.Booking;
using AirlineReservation.Application.DTOs.Seat;
using AirlineReservation.Application.Features.Bookings.Commands.Checkout;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using AirlineReservation.Infrastructure.Context;
using AirlineReservation.Infrastructure.Identity.Entities;
using AirlineReservation.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AirlineReservation.IntegrationTests.Controllers;

/// <summary>
/// Integration tests verifying atomic hold claim before charging, compensation, and edge-case checkout invariants.
/// </summary>
public class CheckoutAtomicClaimIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private int _testFlightId;
    private List<Seat> _testSeats = [];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public CheckoutAtomicClaimIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseCreatedAsync();
        await _factory.ClearTestDataAsync();
        await _factory.SeedTestDataAsync();

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var airline = await context.Airlines.FirstAsync();
        var dep = await context.Airports.FirstAsync(a => a.IataCode == "GYD");
        var arr = await context.Airports.FirstAsync(a => a.IataCode == "IST");

        var aircraft = new Aircraft
        {
            Model = "Airbus A320-200",
            TailNumber = $"CLM-{Guid.NewGuid():N}"[..12],
            TotalSeats = 30,
            AirlineId = airline.Id
        };
        context.Aircrafts.Add(aircraft);
        await context.SaveChangesAsync();

        var seats = new List<Seat>();
        for (int row = 1; row <= 5; row++)
        {
            foreach (var col in new[] { "A", "B", "C", "D", "E", "F" })
            {
                seats.Add(new Seat
                {
                    AircraftId = aircraft.Id,
                    SeatNumber = $"{row}{col}",
                    RowNumber = row,
                    ColumnLetter = col,
                    SeatClass = SeatClass.Economy,
                    BasePriceMultiplier = 1.0m,
                    IsWindow = col is "A" or "F",
                    IsAisle = col is "C" or "D"
                });
            }
        }
        context.Seats.AddRange(seats);
        await context.SaveChangesAsync();

        var depTime = _factory.Clock.GetUtcNow().UtcDateTime.Date.AddDays(5).AddHours(10);
        var flight = new Flight
        {
            FlightNumber = $"J2-{Random.Shared.Next(1000, 9999)}",
            AirlineId = airline.Id,
            AircraftId = aircraft.Id,
            DepartureAirportId = dep.Id,
            ArrivalAirportId = arr.Id,
            DepartureTimeUtc = depTime,
            ArrivalTimeUtc = depTime.AddHours(3),
            BasePrice = 120m,
            Stops = 0,
            Status = FlightStatus.Scheduled
        };
        context.Flights.Add(flight);
        await context.SaveChangesAsync();

        _testFlightId = flight.Id;
        _testSeats = seats;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(AppUser User, string Token)> CreateUserAndTokenAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = "Test",
            LastName = "User",
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, "Password123!");
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        var token = tokenService.GenerateToken(new TokenUserInfo(user.Id, user.Email!, user.FirstName, user.LastName, ["Customer"]));
        return (user, token.AccessToken);
    }

    private static CheckoutCommand CreateCheckoutCommand(int flightId, List<int> seatIds)
    {
        return new CheckoutCommand
        {
            FlightId = flightId,
            Passengers = seatIds.Select((sid, i) => new CheckoutPassengerDto
            {
                FirstName = "Ali",
                LastName = "Aliyev",
                PassportNumber = $"C{i + 1:D8}",
                DateOfBirth = new DateTime(1995, 5, 20),
                SeatId = sid
            }).ToList(),
            Contact = new CheckoutContactDto
            {
                Email = "double@example.com",
                PhoneNumber = "+994501234567"
            },
            Card = new CardPaymentDto
            {
                CardNumber = MockPaymentGateway.SuccessTestCard,
                CardHolderName = "ALI ALIYEV",
                ExpiryMonthYear = "12/28",
                Cvv = "123"
            }
        };
    }

    private sealed class CountingPaymentGateway : IPaymentGateway
    {
        private readonly MockPaymentGateway _inner;
        private readonly int _delayMs;
        private int _chargeCount;

        public CountingPaymentGateway(TimeProvider timeProvider, int delayMs = 150)
        {
            _inner = new MockPaymentGateway(timeProvider);
            _delayMs = delayMs;
        }

        public int ChargeCount => Volatile.Read(ref _chargeCount);
        public IReadOnlyCollection<string> ReversedTransactions => _inner.ReversedTransactions;

        public async Task<PaymentProcessResult> ProcessPaymentAsync(
            PaymentProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _chargeCount);
            if (_delayMs > 0)
            {
                await Task.Delay(_delayMs, cancellationToken);
            }

            return await _inner.ProcessPaymentAsync(request, cancellationToken);
        }

        public Task<PaymentReversalResult> ReverseAsync(
            PaymentReversalRequest request,
            CancellationToken cancellationToken = default)
        {
            return _inner.ReverseAsync(request, cancellationToken);
        }
    }

    [Fact]
    public async Task Checkout_SameUserDoubleSubmit_ChargesPaymentExactlyOnce_AndCreatesSingleBooking()
    {
        var countingGateway = new CountingPaymentGateway(_factory.Clock, delayMs: 200);
        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPaymentGateway>();
                services.AddSingleton<IPaymentGateway>(countingGateway);
            });
        });

        var (user, token) = await CreateUserAndTokenAsync("double_submit@airline.test");
        var client1 = customFactory.CreateClient();
        client1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var client2 = customFactory.CreateClient();
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var seat = _testSeats[0];

        // 1. User holds the seat
        var holdResp = await client1.PostAsJsonAsync(
            $"/api/v1/flights/{_testFlightId}/holds",
            new SeatHoldRequestDto([seat.Id]));
        Assert.Equal(HttpStatusCode.OK, holdResp.StatusCode);

        // 2. Fire two simultaneous checkout requests from the SAME user (double-click simulation)
        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cmd1 = CreateCheckoutCommand(_testFlightId, [seat.Id]);
        var cmd2 = CreateCheckoutCommand(_testFlightId, [seat.Id]);

        var t1 = Task.Run(async () =>
        {
            await startGate.Task;
            return await client1.PostAsJsonAsync("/api/v1/bookings/checkout", cmd1);
        });
        var t2 = Task.Run(async () =>
        {
            await startGate.Task;
            return await client2.PostAsJsonAsync("/api/v1/bookings/checkout", cmd2);
        });

        startGate.SetResult();
        var responses = await Task.WhenAll(t1, t2);

        // 3. Assert: exactly ONE payment charge occurred (no double charge!)
        Assert.Equal(1, countingGateway.ChargeCount);

        // 4. Assert: exactly ONE booking and ONE payment in DB for this user
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userBookings = await context.Bookings.Where(b => b.UserId == user.Id).ToListAsync();
        Assert.Single(userBookings);

        var booking = userBookings[0];
        var paymentsCount = await context.Payments.CountAsync(p => p.BookingId == booking.Id);
        Assert.Equal(1, paymentsCount);

        // 5. Assert: one succeeded, one got 409 Conflict
        var statuses = responses.Select(r => r.StatusCode).ToList();
        Assert.Contains(HttpStatusCode.Conflict, statuses);
        Assert.Single(statuses, s => s is HttpStatusCode.Created);
    }

    [Fact]
    public async Task Checkout_DeclinedPayment_RollsBackClaim_PreservingHoldsForRetry()
    {
        var (user, token) = await CreateUserAndTokenAsync("declined_retry@airline.test");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var seat = _testSeats[1];

        // 1. Hold seat
        var holdResp = await client.PostAsJsonAsync(
            $"/api/v1/flights/{_testFlightId}/holds",
            new SeatHoldRequestDto([seat.Id]));
        Assert.Equal(HttpStatusCode.OK, holdResp.StatusCode);

        // 2. Checkout with insufficient funds card
        var declinedCmd = CreateCheckoutCommand(_testFlightId, [seat.Id]);
        declinedCmd.Card.CardNumber = MockPaymentGateway.InsufficientFundsTestCard;

        var declinedResp = await client.PostAsJsonAsync("/api/v1/bookings/checkout", declinedCmd);
        Assert.Equal(HttpStatusCode.BadRequest, declinedResp.StatusCode);

        // 3. Verify hold is still present in database (rollback restored it)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var holdExists = await db.SeatHolds.AnyAsync(h => h.FlightId == _testFlightId && h.SeatId == seat.Id && h.UserId == user.Id);
            Assert.True(holdExists, "Seat hold must be preserved after declined payment rollback.");
        }

        // 4. Retry with valid card -> must succeed with 201 Created
        var successCmd = CreateCheckoutCommand(_testFlightId, [seat.Id]);
        successCmd.Card.CardNumber = MockPaymentGateway.SuccessTestCard;

        var successResp = await client.PostAsJsonAsync("/api/v1/bookings/checkout", successCmd);
        Assert.Equal(HttpStatusCode.Created, successResp.StatusCode);

        var successBody = await successResp.Content.ReadFromJsonAsync<ApiResponse<CheckoutResultDto>>(JsonOptions);
        Assert.NotNull(successBody?.Data);
        Assert.Equal("Confirmed", successBody.Data.Status);
    }

    [Fact]
    public async Task Checkout_ExpectedTotalAmount_Mismatch_Returns409PriceChanged_WithoutCharging()
    {
        var countingGateway = new CountingPaymentGateway(_factory.Clock, delayMs: 0);
        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPaymentGateway>();
                services.AddSingleton<IPaymentGateway>(countingGateway);
            });
        });

        var (user, token) = await CreateUserAndTokenAsync("price_changed@airline.test");
        var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var seat = _testSeats[2];

        // 1. Hold seat
        var holdResp = await client.PostAsJsonAsync(
            $"/api/v1/flights/{_testFlightId}/holds",
            new SeatHoldRequestDto([seat.Id]));
        Assert.Equal(HttpStatusCode.OK, holdResp.StatusCode);

        // 2. Checkout with mismatched expected price (e.g. 50 AZN when actual is 120 AZN)
        var cmd = CreateCheckoutCommand(_testFlightId, [seat.Id]);
        cmd.ExpectedTotalAmount = 50.00m;

        var resp = await client.PostAsJsonAsync("/api/v1/bookings/checkout", cmd);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<ApiResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Contains("PriceChanged", body.Message);

        // 3. Assert: Payment gateway was NEVER called
        Assert.Equal(0, countingGateway.ChargeCount);

        // 4. Assert: Hold was preserved in DB (transaction rolled back)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var holdExists = await db.SeatHolds.AnyAsync(h => h.FlightId == _testFlightId && h.SeatId == seat.Id && h.UserId == user.Id);
            Assert.True(holdExists, "Seat hold must be preserved after price mismatch.");
        }
    }

    [Fact]
    public async Task Checkout_PassengerSeatsMismatchWithHolds_Returns409_WithoutCharging()
    {
        var countingGateway = new CountingPaymentGateway(_factory.Clock, delayMs: 0);
        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPaymentGateway>();
                services.AddSingleton<IPaymentGateway>(countingGateway);
            });
        });

        var (user, token) = await CreateUserAndTokenAsync("mismatch_seats@airline.test");
        var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var heldSeat = _testSeats[3];
        var unheldSeat = _testSeats[4];

        // 1. Hold only heldSeat
        var holdResp = await client.PostAsJsonAsync(
            $"/api/v1/flights/{_testFlightId}/holds",
            new SeatHoldRequestDto([heldSeat.Id]));
        Assert.Equal(HttpStatusCode.OK, holdResp.StatusCode);

        // 2. Checkout sending unheldSeat
        var cmd = CreateCheckoutCommand(_testFlightId, [unheldSeat.Id]);
        var resp = await client.PostAsJsonAsync("/api/v1/bookings/checkout", cmd);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Equal(0, countingGateway.ChargeCount);
    }

    [Fact]
    public async Task Checkout_ExpiredHold_Returns409ExpiredMessage_WithoutCharging()
    {
        var countingGateway = new CountingPaymentGateway(_factory.Clock, delayMs: 0);
        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPaymentGateway>();
                services.AddSingleton<IPaymentGateway>(countingGateway);
            });
        });

        var (user, token) = await CreateUserAndTokenAsync("expired_hold@airline.test");
        var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var seat = _testSeats[5];

        // 1. Create an already expired hold directly in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.SeatHolds.Add(new SeatHold
            {
                FlightId = _testFlightId,
                SeatId = seat.Id,
                UserId = user.Id,
                CreatedAtUtc = _factory.Clock.GetUtcNow().UtcDateTime.AddMinutes(-20),
                ExpiresAtUtc = _factory.Clock.GetUtcNow().UtcDateTime.AddMinutes(-10)
            });
            await db.SaveChangesAsync();
        }

        // 2. Checkout on expired hold
        var cmd = CreateCheckoutCommand(_testFlightId, [seat.Id]);
        var resp = await client.PostAsJsonAsync("/api/v1/bookings/checkout", cmd);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Contains("expired", body.Message, StringComparison.OrdinalIgnoreCase);

        // 3. Payment was NOT charged
        Assert.Equal(0, countingGateway.ChargeCount);
    }

    private sealed class ReversingPaymentGateway : IPaymentGateway
    {
        private readonly MockPaymentGateway _inner;
        public List<PaymentReversalRequest> ReversalCalls { get; } = [];

        public ReversingPaymentGateway(TimeProvider timeProvider)
        {
            _inner = new MockPaymentGateway(timeProvider);
        }

        public Task<PaymentProcessResult> ProcessPaymentAsync(PaymentProcessRequest request, CancellationToken cancellationToken = default)
        {
            return _inner.ProcessPaymentAsync(request, cancellationToken);
        }

        public Task<PaymentReversalResult> ReverseAsync(PaymentReversalRequest request, CancellationToken cancellationToken = default)
        {
            ReversalCalls.Add(request);
            return _inner.ReverseAsync(request, cancellationToken);
        }
    }

    [Fact]
    public async Task Checkout_PaymentProcessRequest_ToString_MasksCardNumber_AndRedactsCvv()
    {
        var req = new PaymentProcessRequest(
            150m,
            "AZN",
            "4111 1111 1111 1111",
            "12/28",
            "789",
            "Ali Aliyev");

        var str = req.ToString();

        Assert.DoesNotContain("4111 1111", str);
        Assert.DoesNotContain("789", str);
        Assert.Contains("1111", str);
        Assert.Contains("***", str);
    }

    [Fact]
    public async Task Checkout_ConcurrentTwoUsers_20Rounds_ExactlyOneChargePerRound()
    {
        for (int round = 0; round < 20; round++)
        {
            var countingGateway = new CountingPaymentGateway(_factory.Clock, delayMs: 20);
            using var customFactory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IPaymentGateway>();
                    services.AddSingleton<IPaymentGateway>(countingGateway);
                });
            });

            var (userA, tokenA) = await CreateUserAndTokenAsync($"user_a_r{round}@test.com");
            var (userB, tokenB) = await CreateUserAndTokenAsync($"user_b_r{round}@test.com");

            var clientA = customFactory.CreateClient();
            clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

            var clientB = customFactory.CreateClient();
            clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

            // Use seat index modulo test seats count
            var seat = _testSeats[round % _testSeats.Count];

            // User A holds the seat
            var holdA = await clientA.PostAsJsonAsync($"/api/v1/flights/{_testFlightId}/holds", new SeatHoldRequestDto([seat.Id]));
            Assert.Equal(HttpStatusCode.OK, holdA.StatusCode);

            // Both attempt checkout simultaneously for this seat
            var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var cmdA = CreateCheckoutCommand(_testFlightId, [seat.Id]);
            var cmdB = CreateCheckoutCommand(_testFlightId, [seat.Id]);

            var tA = Task.Run(async () =>
            {
                await startGate.Task;
                return await clientA.PostAsJsonAsync("/api/v1/bookings/checkout", cmdA);
            });
            var tB = Task.Run(async () =>
            {
                await startGate.Task;
                return await clientB.PostAsJsonAsync("/api/v1/bookings/checkout", cmdB);
            });

            startGate.SetResult();
            var responses = await Task.WhenAll(tA, tB);

            // Exactly ONE payment charge occurred across both requests
            Assert.Equal(1, countingGateway.ChargeCount);

            var statuses = responses.Select(r => r.StatusCode).ToList();
            Assert.Contains(HttpStatusCode.Created, statuses);
            Assert.Contains(HttpStatusCode.Conflict, statuses);

            // Clean up bookings for next round
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tickets = await db.Tickets.Where(t => t.FlightId == _testFlightId && t.SeatId == seat.Id).ToListAsync();
            db.Tickets.RemoveRange(tickets);
            var bIds = tickets.Select(t => t.BookingId).Distinct().ToList();
            var bks = await db.Bookings.Where(b => bIds.Contains(b.Id)).ToListAsync();
            db.Bookings.RemoveRange(bks);
            await db.SaveChangesAsync();
        }
    }
}
