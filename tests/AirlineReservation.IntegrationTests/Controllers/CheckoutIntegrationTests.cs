using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Identity;
using AirlineReservation.Application.DTOs.Booking;
using AirlineReservation.Application.DTOs.Flight;
using AirlineReservation.Application.DTOs.Seat;
using AirlineReservation.Application.Features.Bookings.Commands.Checkout;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using AirlineReservation.Infrastructure.Context;
using AirlineReservation.Infrastructure.Identity.Entities;
using AirlineReservation.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AirlineReservation.IntegrationTests.Controllers;

public class CheckoutIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private int _testFlightId;
    private List<Seat> _testSeats = [];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public CheckoutIntegrationTests(CustomWebApplicationFactory factory)
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
            TailNumber = $"TEST-{Guid.NewGuid():N}"[..12],
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

        var departureDate = _factory.Clock.GetUtcNow().UtcDateTime.Date.AddDays(5);
        var depTime = departureDate.AddHours(10);
        var arrTime = depTime.AddHours(3);

        var flight = new Flight
        {
            FlightNumber = $"J2-{Random.Shared.Next(1000, 9999)}",
            AirlineId = airline.Id,
            AircraftId = aircraft.Id,
            DepartureAirportId = dep.Id,
            ArrivalAirportId = arr.Id,
            DepartureTimeUtc = depTime,
            ArrivalTimeUtc = arrTime,
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
            LastName = "Passenger",
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, "Password123!");
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to create test user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        var token = tokenService.GenerateToken(new TokenUserInfo(user.Id, user.Email!, user.FirstName, user.LastName, ["Customer"]));
        return (user, token.AccessToken);
    }

    private static CheckoutCommand CreateValidCheckoutCommand(int flightId, List<int> seatIds)
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

    [Fact]
    public async Task Checkout_AnonymousUser_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();
        var command = CreateValidCheckoutCommand(_testFlightId, [_testSeats[0].Id]);

        var response = await client.PostAsJsonAsync("/api/v1/bookings/checkout", command);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_WithoutActiveHold_Returns409Conflict()
    {
        var (_, token) = await CreateUserAndTokenAsync("nohold_user@airline.test");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var command = CreateValidCheckoutCommand(_testFlightId, [_testSeats[0].Id]);

        var response = await client.PostAsJsonAsync("/api/v1/bookings/checkout", command);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(body.Success);
        Assert.Contains("Active seat hold required", body.Message);
    }

    [Fact]
    public async Task Checkout_WithExpiredHold_Returns409Conflict()
    {
        var (_, token) = await CreateUserAndTokenAsync("expired_user@airline.test");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var targetSeat = _testSeats[1];

        // 1. Hold seat
        var holdResponse = await client.PostAsJsonAsync(
            $"/api/v1/flights/{_testFlightId}/holds",
            new SeatHoldRequestDto([targetSeat.Id]));
        Assert.Equal(HttpStatusCode.OK, holdResponse.StatusCode);

        // 2. Advance clock past the 10-minute hold window
        _factory.Clock.Advance(TimeSpan.FromMinutes(11));

        // 3. Attempt checkout on expired hold
        var command = CreateValidCheckoutCommand(_testFlightId, [targetSeat.Id]);
        var response = await client.PostAsJsonAsync("/api/v1/bookings/checkout", command);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(body.Success);
        Assert.Contains("Active seat hold required", body.Message);
    }

    [Fact]
    public async Task Checkout_WithForeignHold_Returns409Conflict()
    {
        var (_, tokenA) = await CreateUserAndTokenAsync("usera_hold@airline.test");
        var (_, tokenB) = await CreateUserAndTokenAsync("userb_checkout@airline.test");

        var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        var targetSeat = _testSeats[2];

        // User A holds the seat
        var holdResponse = await clientA.PostAsJsonAsync(
            $"/api/v1/flights/{_testFlightId}/holds",
            new SeatHoldRequestDto([targetSeat.Id]));
        Assert.Equal(HttpStatusCode.OK, holdResponse.StatusCode);

        // User B attempts to checkout User A's seat
        var command = CreateValidCheckoutCommand(_testFlightId, [targetSeat.Id]);
        var response = await clientB.PostAsJsonAsync("/api/v1/bookings/checkout", command);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(body.Success);
        Assert.Contains("Active seat hold required", body.Message);
    }

    [Fact]
    public async Task Checkout_HappyPath_Returns200OK_PersistsBookingTicketsPayment_AndReleasesHold()
    {
        var (user, token) = await CreateUserAndTokenAsync("happypath@airline.test");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var seatA = _testSeats[3];
        var seatB = _testSeats[4];
        var seatIds = new List<int> { seatA.Id, seatB.Id };

        // 1. Hold 2 seats
        var holdResponse = await client.PostAsJsonAsync(
            $"/api/v1/flights/{_testFlightId}/holds",
            new SeatHoldRequestDto(seatIds));
        Assert.Equal(HttpStatusCode.OK, holdResponse.StatusCode);

        // 2. Execute checkout
        var command = CreateValidCheckoutCommand(_testFlightId, seatIds);
        var response = await client.PostAsJsonAsync("/api/v1/bookings/checkout", command);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CheckoutResultDto>>(JsonOptions);
        Assert.NotNull(body);
        Assert.True(body.Success);
        Assert.NotNull(body.Data);
        Assert.Equal(6, body.Data.PnrCode.Length);
        Assert.Equal("Confirmed", body.Data.Status);
        Assert.True(body.Data.TotalAmount > 0);

        // 3. Verify Database Persistence
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Booking
            var booking = await db.Bookings
                .Include(b => b.Tickets)
                .Include(b => b.Payment)
                .FirstOrDefaultAsync(b => b.PnrCode == body.Data.PnrCode);

            Assert.NotNull(booking);
            Assert.Equal(user.Id, booking.UserId);
            Assert.Equal(BookingStatus.Confirmed, booking.Status);
            Assert.Equal(body.Data.TotalAmount, booking.TotalAmount);

            // Payment
            Assert.NotNull(booking.Payment);
            Assert.Equal(PaymentStatus.Success, booking.Payment.Status);
            Assert.Equal(booking.TotalAmount, booking.Payment.Amount);

            // Tickets
            Assert.Equal(2, booking.Tickets.Count);
            foreach (var ticket in booking.Tickets)
            {
                Assert.Equal(_testFlightId, ticket.FlightId);
                Assert.Contains(ticket.SeatId, seatIds);
                Assert.False(ticket.IsCancelled);
                Assert.StartsWith($"ET-{body.Data.PnrCode}-", ticket.TicketNumber);
            }

            // Holds consumed (deleted)
            var remainingHolds = await db.SeatHolds
                .Where(h => h.FlightId == _testFlightId && h.UserId == user.Id)
                .ToListAsync();
            Assert.Empty(remainingHolds);
        }

        // 4. Verify SeatMap reflects seats as Occupied
        var seatMapResponse = await client.GetAsync($"/api/v1/flights/{_testFlightId}/seatmap");
        Assert.Equal(HttpStatusCode.OK, seatMapResponse.StatusCode);
        var seatMapBody = await seatMapResponse.Content.ReadFromJsonAsync<ApiResponse<SeatMapDto>>(JsonOptions);
        Assert.NotNull(seatMapBody?.Data);

        var allSeats = seatMapBody.Data.Rows.SelectMany(r => r.Seats).ToList();
        var seatAStatus = allSeats.First(s => s.Id == seatA.Id).Status;
        var seatBStatus = allSeats.First(s => s.Id == seatB.Id).Status;

        Assert.Equal(SeatStatus.Occupied, seatAStatus);
        Assert.Equal(SeatStatus.Occupied, seatBStatus);
    }

    [Fact]
    public async Task Checkout_ConcurrentRace_ExactlyOneSucceeds_AndOneGets409Conflict()
    {
        var (user, token) = await CreateUserAndTokenAsync("race_user@airline.test");
        var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var targetSeat = _testSeats[5];

        // 1. User holds seat
        var holdResponse = await clientA.PostAsJsonAsync(
            $"/api/v1/flights/{_testFlightId}/holds",
            new SeatHoldRequestDto([targetSeat.Id]));
        Assert.Equal(HttpStatusCode.OK, holdResponse.StatusCode);

        // 2. Fire 2 checkout commands concurrently
        var commandA = CreateValidCheckoutCommand(_testFlightId, [targetSeat.Id]);
        var commandB = CreateValidCheckoutCommand(_testFlightId, [targetSeat.Id]);

        var taskA = clientA.PostAsJsonAsync("/api/v1/bookings/checkout", commandA);
        var taskB = clientB.PostAsJsonAsync("/api/v1/bookings/checkout", commandB);

        var responses = await Task.WhenAll(taskA, taskB);

        var statusCodes = responses.Select(r => r.StatusCode).ToList();

        // Exactly one should succeed with 200, the other should receive 409 Conflict
        Assert.Contains(HttpStatusCode.OK, statusCodes);
        Assert.Contains(HttpStatusCode.Conflict, statusCodes);

        // Verify only 1 booking and 1 ticket created in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tickets = await db.Tickets
            .Where(t => t.FlightId == _testFlightId && t.SeatId == targetSeat.Id && !t.IsCancelled)
            .ToListAsync();

        Assert.Single(tickets);
    }

    [Fact]
    public async Task Checkout_ValidationFailure_CardDataRedactedFromResponse()
    {
        var (_, token) = await CreateUserAndTokenAsync("security_card@airline.test");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        const string sensitiveCardNumber = "4111111111111111";
        const string sensitiveCvv = "999";

        var command = new CheckoutCommand
        {
            FlightId = _testFlightId,
            Passengers =
            [
                new CheckoutPassengerDto
                {
                    FirstName = "Test",
                    LastName = "Passenger",
                    PassportNumber = "C12345678",
                    DateOfBirth = new DateTime(1990, 1, 1),
                    SeatId = _testSeats[0].Id
                }
            ],
            Contact = new CheckoutContactDto
            {
                Email = "test@example.com",
                PhoneNumber = "+994501234567"
            },
            Card = new CardPaymentDto
            {
                CardNumber = sensitiveCardNumber,
                CardHolderName = "TEST PASSENGER",
                ExpiryMonthYear = "01/20", // Expired card -> triggers validation error
                Cvv = sensitiveCvv
            }
        };

        var response = await client.PostAsJsonAsync("/api/v1/bookings/checkout", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var rawContent = await response.Content.ReadAsStringAsync();

        // PCI DSS assertion: raw card number and CVV must NEVER appear in response body
        Assert.DoesNotContain(sensitiveCardNumber, rawContent);
        Assert.DoesNotContain(sensitiveCvv, rawContent);
    }
}
