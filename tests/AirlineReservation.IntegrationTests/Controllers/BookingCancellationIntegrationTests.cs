using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AirlineReservation.Application.Common.Models;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AirlineReservation.IntegrationTests.Controllers;

public class BookingCancellationIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private int _testFlightId;
    private List<Seat> _testSeats = [];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public BookingCancellationIntegrationTests(CustomWebApplicationFactory factory)
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
            TailNumber = $"TEST-CANC-{Guid.NewGuid():N}"[..12],
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

        var departureTime = _factory.Clock.GetUtcNow().UtcDateTime.AddDays(5);
        var flight = new Flight
        {
            FlightNumber = $"J2-{Random.Shared.Next(1000, 9999)}",
            AirlineId = airline.Id,
            AircraftId = aircraft.Id,
            DepartureAirportId = dep.Id,
            ArrivalAirportId = arr.Id,
            DepartureTimeUtc = departureTime,
            ArrivalTimeUtc = departureTime.AddHours(3),
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

    private async Task<(AppUser User, string Token)> CreateUserAndTokenAsync(string email, string role = "Customer")
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

        var token = tokenService.GenerateToken(new TokenUserInfo(user.Id, user.Email!, user.FirstName, user.LastName, [role]));
        return (user, token.AccessToken);
    }

    private string GenerateTokenForUser(AppUser user, string role = "Customer")
    {
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        return tokenService.GenerateToken(new TokenUserInfo(user.Id, user.Email!, user.FirstName, user.LastName, [role])).AccessToken;
    }

    private async Task<CheckoutResultDto> CreateBookingViaCheckoutAsync(HttpClient client, int flightId, int seatId)
    {
        // 1. Hold seat
        var holdResp = await client.PostAsJsonAsync($"/api/v1/flights/{flightId}/holds", new SeatHoldRequestDto([seatId]));
        Assert.Equal(HttpStatusCode.OK, holdResp.StatusCode);

        // 2. Checkout
        var command = new CheckoutCommand
        {
            FlightId = flightId,
            Passengers =
            [
                new CheckoutPassengerDto
                {
                    FirstName = "Ali",
                    LastName = "Aliyev",
                    PassportNumber = "C12345678",
                    DateOfBirth = new DateTime(1995, 5, 20),
                    SeatId = seatId
                }
            ],
            Contact = new CheckoutContactDto
            {
                Email = "ali@example.com",
                PhoneNumber = "+994501234567"
            },
            Card = new CardPaymentDto
            {
                CardNumber = "4111111111111111",
                CardHolderName = "ALI ALIYEV",
                ExpiryMonthYear = "12/28",
                Cvv = "123"
            }
        };

        var checkoutResp = await client.PostAsJsonAsync("/api/v1/bookings/checkout", command);
        Assert.Equal(HttpStatusCode.OK, checkoutResp.StatusCode);

        var checkoutResult = await checkoutResp.Content.ReadFromJsonAsync<ApiResponse<CheckoutResultDto>>(JsonOptions);
        Assert.NotNull(checkoutResult);
        Assert.True(checkoutResult.Success);
        Assert.NotNull(checkoutResult.Data);

        return checkoutResult.Data;
    }

    [Fact]
    public async Task CancelBooking_Over24HoursBeforeDeparture_CancelsSuccessfullyWithFullRefund_AndLiberatesSeat()
    {
        var targetSeat = _testSeats[0];
        var (_, token) = await CreateUserAndTokenAsync("cancel_full@airline.test");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Create booking
        var checkoutResult = await CreateBookingViaCheckoutAsync(client, _testFlightId, targetSeat.Id);
        var bookingId = checkoutResult.BookingId;

        // 1. Verify seat map shows seat as Occupied
        var seatMapResp1 = await client.GetAsync($"/api/v1/flights/{_testFlightId}/seatmap");
        Assert.Equal(HttpStatusCode.OK, seatMapResp1.StatusCode);
        var seatMap1 = (await seatMapResp1.Content.ReadFromJsonAsync<ApiResponse<SeatMapDto>>(JsonOptions))!.Data!;
        var seatBeforeCancel = seatMap1.Rows.SelectMany(r => r.Seats).First(s => s.Id == targetSeat.Id);
        Assert.Equal(SeatStatus.Occupied, seatBeforeCancel.Status);

        // 2. Cancellation Preview: should show 100% refund
        var previewResp = await client.GetAsync($"/api/v1/bookings/{bookingId}/cancel-preview");
        Assert.Equal(HttpStatusCode.OK, previewResp.StatusCode);
        var previewResult = await previewResp.Content.ReadFromJsonAsync<ApiResponse<CancelBookingPreviewDto>>(JsonOptions);
        Assert.NotNull(previewResult);
        Assert.True(previewResult.Success);
        Assert.True(previewResult.Data!.CanCancel);
        Assert.Equal(100.00m, previewResult.Data.RefundPercentage);
        Assert.Equal(checkoutResult.TotalAmount, previewResult.Data.RefundAmount);
        Assert.Equal(0.00m, previewResult.Data.PenaltyAmount);

        // 3. Cancel the booking
        var cancelResp = await client.PostAsync($"/api/v1/bookings/{bookingId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelResp.StatusCode);
        var cancelResult = await cancelResp.Content.ReadFromJsonAsync<ApiResponse<BookingDetailsDto>>(JsonOptions);
        Assert.NotNull(cancelResult);
        Assert.True(cancelResult.Success);
        Assert.Equal("Cancelled", cancelResult.Data!.Status);
        Assert.Equal(checkoutResult.TotalAmount, cancelResult.Data.RefundAmount);
        Assert.NotNull(cancelResult.Data.CancelledAtUtc);
        Assert.NotNull(cancelResult.Data.Payment);
        Assert.Equal("Refunded", cancelResult.Data.Payment.Status);

        // 4. Verify seat map: seat is now Available!
        var seatMapResp2 = await client.GetAsync($"/api/v1/flights/{_testFlightId}/seatmap");
        Assert.Equal(HttpStatusCode.OK, seatMapResp2.StatusCode);
        var seatMap2 = (await seatMapResp2.Content.ReadFromJsonAsync<ApiResponse<SeatMapDto>>(JsonOptions))!.Data!;
        var seatAfterCancel = seatMap2.Rows.SelectMany(r => r.Seats).First(s => s.Id == targetSeat.Id);
        Assert.Equal(SeatStatus.Available, seatAfterCancel.Status);
    }

    [Fact]
    public async Task CancelBooking_Within24HoursOfDeparture_CancelsWith50PercentRefund()
    {
        var targetSeat = _testSeats[1];
        var (user, token) = await CreateUserAndTokenAsync("cancel_partial@airline.test");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Create booking
        var checkoutResult = await CreateBookingViaCheckoutAsync(client, _testFlightId, targetSeat.Id);
        var bookingId = checkoutResult.BookingId;

        // Advance factory clock to within 12 hours before flight departure
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var flight = await context.Flights.FindAsync(_testFlightId);
            _factory.Clock.SetUtcNow(new DateTimeOffset(flight!.DepartureTimeUtc.AddHours(-12), TimeSpan.Zero));
        }

        // Refresh token for user at new clock time so authentication remains valid
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateTokenForUser(user));

        // Preview: should show 50% refund, 50% penalty
        var previewResp = await client.GetAsync($"/api/v1/bookings/{bookingId}/cancel-preview");
        Assert.Equal(HttpStatusCode.OK, previewResp.StatusCode);
        var previewResult = await previewResp.Content.ReadFromJsonAsync<ApiResponse<CancelBookingPreviewDto>>(JsonOptions);
        Assert.NotNull(previewResult);
        Assert.True(previewResult.Data!.CanCancel);
        Assert.Equal(50.00m, previewResult.Data.RefundPercentage);
        var expectedRefund = Math.Round(checkoutResult.TotalAmount * 0.50m, 2, MidpointRounding.AwayFromZero);
        Assert.Equal(expectedRefund, previewResult.Data.RefundAmount);

        // Cancel
        var cancelResp = await client.PostAsync($"/api/v1/bookings/{bookingId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelResp.StatusCode);
        var cancelResult = await cancelResp.Content.ReadFromJsonAsync<ApiResponse<BookingDetailsDto>>(JsonOptions);
        Assert.NotNull(cancelResult);
        Assert.Equal("Cancelled", cancelResult.Data!.Status);
        Assert.Equal(expectedRefund, cancelResult.Data.RefundAmount);
    }

    [Fact]
    public async Task CancelBooking_PastDeparture_Returns400BadRequest()
    {
        var targetSeat = _testSeats[2];
        var (user, token) = await CreateUserAndTokenAsync("cancel_past@airline.test");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var checkoutResult = await CreateBookingViaCheckoutAsync(client, _testFlightId, targetSeat.Id);
        var bookingId = checkoutResult.BookingId;

        // Advance clock past departure
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var flight = await context.Flights.FindAsync(_testFlightId);
            _factory.Clock.SetUtcNow(new DateTimeOffset(flight!.DepartureTimeUtc.AddHours(2), TimeSpan.Zero));
        }

        // Refresh token for user at new clock time so authentication remains valid
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateTokenForUser(user));

        // Preview shows CanCancel = false
        var previewResp = await client.GetAsync($"/api/v1/bookings/{bookingId}/cancel-preview");
        Assert.Equal(HttpStatusCode.OK, previewResp.StatusCode);
        var previewResult = await previewResp.Content.ReadFromJsonAsync<ApiResponse<CancelBookingPreviewDto>>(JsonOptions);
        Assert.NotNull(previewResult);
        Assert.False(previewResult.Data!.CanCancel);

        // Cancel returns 400 Bad Request
        var cancelResp = await client.PostAsync($"/api/v1/bookings/{bookingId}/cancel", null);
        Assert.Equal(HttpStatusCode.BadRequest, cancelResp.StatusCode);
    }

    [Fact]
    public async Task CancelBooking_NonOwnerNonAdmin_Returns403Forbidden()
    {
        var targetSeat = _testSeats[3];
        var (_, tokenA) = await CreateUserAndTokenAsync("user_owner@airline.test");
        var (_, tokenB) = await CreateUserAndTokenAsync("user_intruder@airline.test");

        var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        var checkoutResult = await CreateBookingViaCheckoutAsync(clientA, _testFlightId, targetSeat.Id);
        var bookingId = checkoutResult.BookingId;

        // User B tries to view preview
        var previewResp = await clientB.GetAsync($"/api/v1/bookings/{bookingId}/cancel-preview");
        Assert.Equal(HttpStatusCode.Forbidden, previewResp.StatusCode);

        // User B tries to cancel User A's booking
        var cancelResp = await clientB.PostAsync($"/api/v1/bookings/{bookingId}/cancel", null);
        Assert.Equal(HttpStatusCode.Forbidden, cancelResp.StatusCode);
    }

    [Fact]
    public async Task CancelBooking_AdminCanCancelAnyUserBooking_Succeeds()
    {
        var targetSeat = _testSeats[4];
        var (_, customerToken) = await CreateUserAndTokenAsync("cust_for_admin@airline.test");
        var (_, adminToken) = await CreateUserAndTokenAsync("admin_canceller@airline.test", role: "Admin");

        var customerClient = _factory.CreateClient();
        customerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);

        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var checkoutResult = await CreateBookingViaCheckoutAsync(customerClient, _testFlightId, targetSeat.Id);
        var bookingId = checkoutResult.BookingId;

        // Admin cancels customer's booking
        var cancelResp = await adminClient.PostAsync($"/api/v1/bookings/{bookingId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelResp.StatusCode);

        var cancelResult = await cancelResp.Content.ReadFromJsonAsync<ApiResponse<BookingDetailsDto>>(JsonOptions);
        Assert.NotNull(cancelResult);
        Assert.True(cancelResult.Success);
        Assert.Equal("Cancelled", cancelResult.Data!.Status);
    }

    [Fact]
    public async Task CancelBooking_AlreadyCancelled_Returns400BadRequest()
    {
        var targetSeat = _testSeats[5];
        var (_, token) = await CreateUserAndTokenAsync("cancel_twice@airline.test");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var checkoutResult = await CreateBookingViaCheckoutAsync(client, _testFlightId, targetSeat.Id);
        var bookingId = checkoutResult.BookingId;

        // Cancel first time
        var cancelResp1 = await client.PostAsync($"/api/v1/bookings/{bookingId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelResp1.StatusCode);

        // Cancel second time
        var cancelResp2 = await client.PostAsync($"/api/v1/bookings/{bookingId}/cancel", null);
        Assert.Equal(HttpStatusCode.BadRequest, cancelResp2.StatusCode);

        var body = await cancelResp2.Content.ReadFromJsonAsync<ApiResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Contains("already cancelled", body.Message);
    }
}
