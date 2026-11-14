using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Identity;
using AirlineReservation.Application.DTOs.Flight;
using AirlineReservation.Application.DTOs.Seat;
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

public class SeatHoldIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private int _testFlightId;
    private int _departureAirportId;
    private int _arrivalAirportId;
    private DateTime _departureDate;
    private List<Seat> _testSeats = [];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public SeatHoldIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ClearTestDataAsync();
        await _factory.SeedTestDataAsync();

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var airline = await context.Airlines.FirstAsync();
        var dep = await context.Airports.FirstAsync(a => a.IataCode == "GYD");
        var arr = await context.Airports.FirstAsync(a => a.IataCode == "IST");
        _departureAirportId = dep.Id;
        _arrivalAirportId = arr.Id;

        var aircraft = new Aircraft
        {
            Model = "Boeing 737-800",
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

        _departureDate = _factory.Clock.GetUtcNow().UtcDateTime.Date.AddDays(5);
        var depTime = _departureDate.AddHours(10);
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

    [Fact]
    public async Task ParallelHold_TwentyRounds_ExactlyOneSuccessAndOneConflictPerRound()
    {
        var (_, tokenA) = await CreateUserAndTokenAsync("parallel_a@airline.test");
        var (_, tokenB) = await CreateUserAndTokenAsync("parallel_b@airline.test");

        var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        int flightId = _testFlightId;
        Assert.True(_testSeats.Count >= 20, "Must have at least 20 seats for 20 parallel rounds.");

        for (int round = 0; round < 20; round++)
        {
            var seat = _testSeats[round];
            var requestBody = new SeatHoldRequestDto(new[] { seat.Id });

            // Start gate ensures simultaneous execution
            var startGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var taskA = Task.Run(async () =>
            {
                await startGate.Task;
                return await clientA.PostAsJsonAsync($"/api/v1/flights/{flightId}/holds", requestBody);
            });

            var taskB = Task.Run(async () =>
            {
                await startGate.Task;
                return await clientB.PostAsJsonAsync($"/api/v1/flights/{flightId}/holds", requestBody);
            });

            startGate.SetResult(true);

            var responses = await Task.WhenAll(taskA, taskB);

            var codes = responses.Select(r => r.StatusCode).ToList();
            Assert.Contains(HttpStatusCode.OK, codes);
            Assert.Contains(HttpStatusCode.Conflict, codes);

            // Database assertion: Strictly exactly ONE hold row in SeatHolds for this flight and seat!
            using var verifyScope = _factory.Services.CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var holdCount = await verifyContext.SeatHolds
                .CountAsync(h => h.FlightId == flightId && h.SeatId == seat.Id);

            Assert.Equal(1, holdCount);
        }
    }

    [Fact]
    public async Task HoldExpiry_AdvancingClockByTenMinutes_ReleasesSeatForOtherPassenger()
    {
        var (_, tokenA) = await CreateUserAndTokenAsync("expiry_a@airline.test");
        var (_, tokenB) = await CreateUserAndTokenAsync("expiry_b@airline.test");

        var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        int flightId = _testFlightId;
        var targetSeat = _testSeats[0];

        // 1. User A holds seat
        var holdResponse = await clientA.PostAsJsonAsync(
            $"/api/v1/flights/{flightId}/holds",
            new SeatHoldRequestDto(new[] { targetSeat.Id }));
        Assert.Equal(HttpStatusCode.OK, holdResponse.StatusCode);

        // 2. Immediate hold attempt by User B conflicts
        var immediateConflict = await clientB.PostAsJsonAsync(
            $"/api/v1/flights/{flightId}/holds",
            new SeatHoldRequestDto(new[] { targetSeat.Id }));
        Assert.Equal(HttpStatusCode.Conflict, immediateConflict.StatusCode);

        // 3. Advance clock by 10 minutes (hold expires)
        _factory.Clock.Advance(TimeSpan.FromMinutes(10));

        // 4. User B holds same seat -> succeeds because expired hold is deleted by Hold rule
        var postExpiryHold = await clientB.PostAsJsonAsync(
            $"/api/v1/flights/{flightId}/holds",
            new SeatHoldRequestDto(new[] { targetSeat.Id }));
        Assert.Equal(HttpStatusCode.OK, postExpiryHold.StatusCode);
    }

    [Fact]
    public async Task ConflictRollback_PreservesUserOriginalHolds()
    {
        var (userA, tokenA) = await CreateUserAndTokenAsync("rollback_a@airline.test");
        var (_, tokenB) = await CreateUserAndTokenAsync("rollback_b@airline.test");

        var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        int flightId = _testFlightId;
        var seat0 = _testSeats[0];
        var seat1 = _testSeats[1];

        // 1. User A holds Seat 0
        var holdA = await clientA.PostAsJsonAsync(
            $"/api/v1/flights/{flightId}/holds",
            new SeatHoldRequestDto(new[] { seat0.Id }));
        Assert.Equal(HttpStatusCode.OK, holdA.StatusCode);

        // 2. User B holds Seat 1
        var holdB = await clientB.PostAsJsonAsync(
            $"/api/v1/flights/{flightId}/holds",
            new SeatHoldRequestDto(new[] { seat1.Id }));
        Assert.Equal(HttpStatusCode.OK, holdB.StatusCode);

        // 3. User A attempts to hold [Seat 0, Seat 1] -> conflicts on Seat 1
        var conflictResponse = await clientA.PostAsJsonAsync(
            $"/api/v1/flights/{flightId}/holds",
            new SeatHoldRequestDto(new[] { seat0.Id, seat1.Id }));
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

        // 4. Assert: Because transaction rolled back, User A's hold on Seat 0 is STILL preserved!
        using var verifyScope = _factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userAHoldOnSeat0 = await verifyContext.SeatHolds
            .FirstOrDefaultAsync(h => h.FlightId == flightId && h.SeatId == seat0.Id && h.UserId == userA.Id);

        Assert.NotNull(userAHoldOnSeat0);
    }

    [Fact]
    public async Task Hold_ForeignSeat_Returns400_AndOccupiedSeat_Returns409()
    {
        var (_, token) = await CreateUserAndTokenAsync("validation_user@airline.test");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        int flightId = _testFlightId;

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Find a seat on ANOTHER aircraft (seeded a320)
        var foreignSeat = await context.Seats.FirstAsync(s => s.AircraftId != _testSeats[0].AircraftId);

        // 1. Foreign seat -> 400 BadRequest
        var foreignResponse = await client.PostAsJsonAsync(
            $"/api/v1/flights/{flightId}/holds",
            new SeatHoldRequestDto(new[] { foreignSeat.Id }));
        Assert.Equal(HttpStatusCode.BadRequest, foreignResponse.StatusCode);

        // 2. Occupied seat with active ticket -> 409 Conflict
        var occupiedSeat = _testSeats.Last();
        var existingUser = await context.Users.FirstAsync();
        var passenger = new Passenger
        {
            FirstName = "Ali",
            LastName = "Aliyev",
            PassportNumber = "AZE12345678",
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = "Male"
        };
        var booking = new Booking
        {
            PnrCode = "HLD409",
            UserId = existingUser.Id,
            ContactEmail = "test@airline.test",
            ContactPhone = "+994501234567",
            TotalAmount = 200m,
            Status = BookingStatus.Confirmed
        };
        var ticket = new Ticket
        {
            FlightId = flightId,
            SeatId = occupiedSeat.Id,
            Passenger = passenger,
            Booking = booking,
            TicketNumber = "TK409001",
            Price = 200m,
            IsCancelled = false
        };
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        var occupiedResponse = await client.PostAsJsonAsync(
            $"/api/v1/flights/{flightId}/holds",
            new SeatHoldRequestDto(new[] { occupiedSeat.Id }));
        Assert.Equal(HttpStatusCode.Conflict, occupiedResponse.StatusCode);
    }

    [Fact]
    public async Task ReleaseMyHolds_IdempotentlyReleasesOnlyOwnHolds()
    {
        var (userA, tokenA) = await CreateUserAndTokenAsync("release_a@airline.test");
        var (userB, tokenB) = await CreateUserAndTokenAsync("release_b@airline.test");

        var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        int flightId = _testFlightId;
        var seatA = _testSeats[0];
        var seatB = _testSeats[1];

        await clientA.PostAsJsonAsync($"/api/v1/flights/{flightId}/holds", new SeatHoldRequestDto(new[] { seatA.Id }));
        await clientB.PostAsJsonAsync($"/api/v1/flights/{flightId}/holds", new SeatHoldRequestDto(new[] { seatB.Id }));

        // User A releases holds
        var deleteResponse = await clientA.DeleteAsync($"/api/v1/flights/{flightId}/holds");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        // Verify User A hold deleted, User B hold intact
        using var verifyScope = _factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userAHolds = await verifyContext.SeatHolds.Where(h => h.UserId == userA.Id).ToListAsync();
        var userBHolds = await verifyContext.SeatHolds.Where(h => h.UserId == userB.Id).ToListAsync();

        Assert.Empty(userAHolds);
        Assert.Single(userBHolds);

        // Second delete by User A -> 200 OK (idempotent)
        var secondDelete = await clientA.DeleteAsync($"/api/v1/flights/{flightId}/holds");
        Assert.Equal(HttpStatusCode.OK, secondDelete.StatusCode);
    }

    [Fact]
    public async Task PayloadSize_StatusEndpoint_IsDramaticallySmallerThanFullSeatMap()
    {
        var client = _factory.CreateClient();
        int flightId = _testFlightId;

        // 1. Full seat map
        var fullMapResponse = await client.GetAsync($"/api/v1/flights/{flightId}/seatmap");
        Assert.Equal(HttpStatusCode.OK, fullMapResponse.StatusCode);
        byte[] fullMapBytes = await fullMapResponse.Content.ReadAsByteArrayAsync();

        // 2. Light status delta
        var statusResponse = await client.GetAsync($"/api/v1/flights/{flightId}/seatmap/status");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        byte[] statusBytes = await statusResponse.Content.ReadAsByteArrayAsync();

        // Status delta payload must be smaller than full seatmap (no rows, no classes, no prices)
        Assert.True(statusBytes.Length < fullMapBytes.Length / 2,
            $"Expected status payload ({statusBytes.Length} bytes) to be < 50% of full seatmap ({fullMapBytes.Length} bytes).");
    }

    [Fact]
    public async Task PublicSeatMap_WithExpiredOrInvalidToken_Returns200WithHeldByMeFalse()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "completely.invalid.jwt-token");

        int flightId = _testFlightId;

        var response = await client.GetAsync($"/api/v1/flights/{flightId}/seatmap");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<SeatMapDto>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.True(payload.Success);
        Assert.All(payload.Data!.Rows.SelectMany(r => r.Seats), s => Assert.False(s.HeldByMe));
    }

    [Fact]
    public async Task FlightSearch_AvailableSeatsCount_EqualsSeatMapAvailablePlusHeld()
    {
        var (_, token) = await CreateUserAndTokenAsync("search_hold@airline.test");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        int flightId = _testFlightId;
        var seatsToHold = _testSeats.Take(3).Select(s => s.Id).ToList();

        // Hold 3 seats
        var holdResp = await client.PostAsJsonAsync($"/api/v1/flights/{flightId}/holds", new SeatHoldRequestDto(seatsToHold));
        Assert.Equal(HttpStatusCode.OK, holdResp.StatusCode);

        // 1. Check SeatMap
        var mapResp = await client.GetAsync($"/api/v1/flights/{flightId}/seatmap");
        var mapPayload = await mapResp.Content.ReadFromJsonAsync<ApiResponse<SeatMapDto>>(JsonOptions);
        Assert.NotNull(mapPayload);

        // 2. Check Search
        string dateStr = _departureDate.ToString("yyyy-MM-dd");
        var searchResp = await client.GetAsync(
            $"/api/v1/flights/search?departureAirportId={_departureAirportId}&arrivalAirportId={_arrivalAirportId}&departureDate={dateStr}");
        var searchPayload = await searchResp.Content.ReadFromJsonAsync<ApiResponse<PagedResult<FlightDto>>>(JsonOptions);
        Assert.NotNull(searchPayload);

        var searchFlight = searchPayload.Data!.Items.First(f => f.Id == flightId);

        // Documented rule: Flight Search AvailableSeatsCount == SeatMap (AvailableSeatsCount + HeldSeatsCount)
        Assert.Equal(
            searchFlight.AvailableSeatsCount,
            mapPayload.Data!.AvailableSeatsCount + mapPayload.Data.HeldSeatsCount);
    }
}
