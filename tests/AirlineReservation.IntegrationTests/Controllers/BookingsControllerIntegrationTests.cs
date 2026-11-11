using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Booking;
using AirlineReservation.Application.DTOs.Seat;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using AirlineReservation.Infrastructure.Context;
using AirlineReservation.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AirlineReservation.IntegrationTests.Controllers;

public class BookingsControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public BookingsControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseCreatedAsync();
        await _factory.ClearTestDataAsync();
        await _factory.SeedTestDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Quote_ValidTwoSeats_ReturnsItemizedBreakdown_MatchingSeatMapPricesAndExactSum()
    {
        int flightId;
        int seat2AId;
        int seat2CId;

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var airlineId = await context.Airlines.Select(a => a.Id).FirstAsync();
            var b738 = await context.Aircrafts.FirstAsync(a => a.TailNumber == "TEST-B738-1");

            var seat2A = new Seat
            {
                AircraftId = b738.Id,
                SeatNumber = "2A",
                RowNumber = 2,
                ColumnLetter = "A",
                SeatClass = SeatClass.Economy,
                BasePriceMultiplier = 1.0m,
                IsWindow = true
            };
            var seat2C = new Seat
            {
                AircraftId = b738.Id,
                SeatNumber = "2C",
                RowNumber = 2,
                ColumnLetter = "C",
                SeatClass = SeatClass.Economy,
                BasePriceMultiplier = 1.0m,
                IsAisle = true
            };
            context.Seats.AddRange(seat2A, seat2C);

            var standardFlight = new Flight
            {
                FlightNumber = "J2-200",
                AirlineId = airlineId,
                AircraftId = b738.Id,
                DepartureAirportId = _factory.TestDepartureAirportId,
                ArrivalAirportId = _factory.TestArrivalAirportId,
                DepartureTimeUtc = new DateTime(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc), // +10d
                ArrivalTimeUtc = new DateTime(2026, 11, 20, 11, 0, 0, DateTimeKind.Utc),
                BasePrice = 100.00m,
                Stops = 0,
                Status = FlightStatus.Scheduled
            };
            context.Flights.Add(standardFlight);
            await context.SaveChangesAsync();

            flightId = standardFlight.Id;
            seat2AId = seat2A.Id;
            seat2CId = seat2C.Id;
        }

        var seatMapResp = await _client.GetAsync($"/api/v1/flights/{flightId}/seatmap");
        Assert.Equal(HttpStatusCode.OK, seatMapResp.StatusCode);
        var seatMap = (await seatMapResp.Content.ReadFromJsonAsync<ApiResponse<SeatMapDto>>(JsonOptions))!.Data!;
        var mapPrices = seatMap.Rows.SelectMany(r => r.Seats).ToDictionary(s => s.Id, s => s.Price);

        var quoteResp = await _client.PostAsJsonAsync(
            "/api/v1/bookings/quote",
            new BookingQuoteRequestDto(flightId, [seat2AId, seat2CId]));

        Assert.Equal(HttpStatusCode.OK, quoteResp.StatusCode);
        var quote = (await quoteResp.Content.ReadFromJsonAsync<ApiResponse<BookingQuoteDto>>(JsonOptions))!.Data!;

        Assert.Equal(2, quote.Items.Count);
        Assert.Equal(mapPrices[seat2AId], quote.Items[0].FinalPrice);
        Assert.Equal(mapPrices[seat2CId], quote.Items[1].FinalPrice);
        Assert.Equal(110.00m, quote.Items[0].FinalPrice);
        Assert.Equal(105.00m, quote.Items[1].FinalPrice);
        Assert.Equal(215.00m, quote.TotalPrice);
        Assert.Equal(quote.Items.Sum(i => i.FinalPrice), quote.TotalPrice);
    }

    [Fact]
    public async Task Quote_LastMinuteDelayedFlight_AppliesDemandMultiplier_AndMatchesSeatMap()
    {
        int seat2AId;
        int seat2BId;
        int delayedFlightId = _factory.TestFlight2DelayedId; // BasePrice=200m, +10h <= 72h

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var b738 = await context.Aircrafts.FirstAsync(a => a.TailNumber == "TEST-B738-1");

            var seat2A = new Seat
            {
                AircraftId = b738.Id,
                SeatNumber = "2A",
                RowNumber = 2,
                ColumnLetter = "A",
                SeatClass = SeatClass.Economy,
                BasePriceMultiplier = 1.0m,
                IsWindow = true
            };
            var seat2B = new Seat
            {
                AircraftId = b738.Id,
                SeatNumber = "2B",
                RowNumber = 2,
                ColumnLetter = "B",
                SeatClass = SeatClass.Economy,
                BasePriceMultiplier = 1.0m
            };
            context.Seats.AddRange(seat2A, seat2B);
            await context.SaveChangesAsync();

            seat2AId = seat2A.Id;
            seat2BId = seat2B.Id;
        }

        var seatMapResp = await _client.GetAsync($"/api/v1/flights/{delayedFlightId}/seatmap");
        Assert.Equal(HttpStatusCode.OK, seatMapResp.StatusCode);
        var seatMap = (await seatMapResp.Content.ReadFromJsonAsync<ApiResponse<SeatMapDto>>(JsonOptions))!.Data!;
        var mapPrices = seatMap.Rows.SelectMany(r => r.Seats).ToDictionary(s => s.Id, s => s.Price);

        var quoteResp = await _client.PostAsJsonAsync(
            "/api/v1/bookings/quote",
            new BookingQuoteRequestDto(delayedFlightId, [seat2AId, seat2BId]));

        Assert.Equal(HttpStatusCode.OK, quoteResp.StatusCode);
        var quote = (await quoteResp.Content.ReadFromJsonAsync<ApiResponse<BookingQuoteDto>>(JsonOptions))!.Data!;

        // 2A: (200 + 10) * 1.20 = 252.00; 2B: 200 * 1.20 = 240.00
        Assert.True(quote.Items[0].IsLastMinuteDemandApplied);
        Assert.Equal(1.20m, quote.Items[0].DemandMultiplier);
        Assert.Equal(252.00m, quote.Items[0].FinalPrice);
        Assert.Equal(240.00m, quote.Items[1].FinalPrice);
        Assert.Equal(mapPrices[seat2AId], quote.Items[0].FinalPrice);
        Assert.Equal(mapPrices[seat2BId], quote.Items[1].FinalPrice);
        Assert.Equal(492.00m, quote.TotalPrice);
    }

    [Fact]
    public async Task Quote_SeatFromDifferentAircraft_Returns400()
    {
        int foreignSeatId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var b738 = await context.Aircrafts.FirstAsync(a => a.TailNumber == "TEST-B738-1");
            var foreignSeat = new Seat
            {
                AircraftId = b738.Id,
                SeatNumber = "5A",
                RowNumber = 5,
                ColumnLetter = "A",
                SeatClass = SeatClass.Economy
            };
            context.Seats.Add(foreignSeat);
            await context.SaveChangesAsync();
            foreignSeatId = foreignSeat.Id;
        }

        // TestFlight1Id uses a320, foreignSeatId belongs to b738
        var resp = await _client.PostAsJsonAsync(
            "/api/v1/bookings/quote",
            new BookingQuoteRequestDto(_factory.TestFlight1Id, [foreignSeatId]));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Quote_UnknownFlightOrSeat_Returns404()
    {
        int validSeatId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            validSeatId = await context.Seats.Where(s => s.SeatNumber == "1B").Select(s => s.Id).FirstAsync();
        }

        var unknownFlightResp = await _client.PostAsJsonAsync(
            "/api/v1/bookings/quote",
            new BookingQuoteRequestDto(999999, [validSeatId]));
        Assert.Equal(HttpStatusCode.NotFound, unknownFlightResp.StatusCode);

        var unknownSeatResp = await _client.PostAsJsonAsync(
            "/api/v1/bookings/quote",
            new BookingQuoteRequestDto(_factory.TestFlight1Id, [999999]));
        Assert.Equal(HttpStatusCode.NotFound, unknownSeatResp.StatusCode);
    }

    [Fact]
    public async Task Quote_CancelledOrPastFlight_Returns409()
    {
        int b738SeatId;
        int pastFlightId;

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var airlineId = await context.Airlines.Select(a => a.Id).FirstAsync();
            var b738 = await context.Aircrafts.FirstAsync(a => a.TailNumber == "TEST-B738-1");
            var seat = new Seat
            {
                AircraftId = b738.Id,
                SeatNumber = "6A",
                RowNumber = 6,
                ColumnLetter = "A",
                SeatClass = SeatClass.Economy
            };
            context.Seats.Add(seat);

            var pastFlight = new Flight
            {
                FlightNumber = "J2-PAST",
                AirlineId = airlineId,
                AircraftId = b738.Id,
                DepartureAirportId = _factory.TestDepartureAirportId,
                ArrivalAirportId = _factory.TestArrivalAirportId,
                // Before FixedTimeProvider (2026-11-10T00:00:00Z)
                DepartureTimeUtc = new DateTime(2026, 11, 9, 22, 0, 0, DateTimeKind.Utc),
                ArrivalTimeUtc = new DateTime(2026, 11, 10, 1, 0, 0, DateTimeKind.Utc),
                BasePrice = 120.00m,
                Stops = 0,
                Status = FlightStatus.Scheduled
            };
            context.Flights.Add(pastFlight);
            await context.SaveChangesAsync();

            b738SeatId = seat.Id;
            pastFlightId = pastFlight.Id;
        }

        var cancelledResp = await _client.PostAsJsonAsync(
            "/api/v1/bookings/quote",
            new BookingQuoteRequestDto(_factory.TestFlight3CancelledId, [b738SeatId]));
        Assert.Equal(HttpStatusCode.Conflict, cancelledResp.StatusCode);

        var pastResp = await _client.PostAsJsonAsync(
            "/api/v1/bookings/quote",
            new BookingQuoteRequestDto(pastFlightId, [b738SeatId]));
        Assert.Equal(HttpStatusCode.Conflict, pastResp.StatusCode);
    }

    [Fact]
    public async Task Quote_OccupiedSeat_Returns409AndListsConflictingSeatNumber()
    {
        int seat1AId;
        int seat1BId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            seat1AId = await context.Seats.Where(s => s.SeatNumber == "1A").Select(s => s.Id).FirstAsync();
            seat1BId = await context.Seats.Where(s => s.SeatNumber == "1B").Select(s => s.Id).FirstAsync();
        }

        var resp = await _client.PostAsJsonAsync(
            "/api/v1/bookings/quote",
            new BookingQuoteRequestDto(_factory.TestFlight1Id, [seat1AId, seat1BId]));

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("1A", body);
    }

    [Fact]
    public async Task Quote_InvalidSeatList_Returns400ValidationError()
    {
        int seat1BId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            seat1BId = await context.Seats.Where(s => s.SeatNumber == "1B").Select(s => s.Id).FirstAsync();
        }

        var emptyResp = await _client.PostAsJsonAsync(
            "/api/v1/bookings/quote",
            new BookingQuoteRequestDto(_factory.TestFlight1Id, []));
        Assert.Equal(HttpStatusCode.BadRequest, emptyResp.StatusCode);

        var duplicateResp = await _client.PostAsJsonAsync(
            "/api/v1/bookings/quote",
            new BookingQuoteRequestDto(_factory.TestFlight1Id, [seat1BId, seat1BId]));
        Assert.Equal(HttpStatusCode.BadRequest, duplicateResp.StatusCode);
    }

    [Theory]
    [InlineData("Pricing:WindowFee", "-1.00")]
    [InlineData("Pricing:AisleFee", "-0.50")]
    [InlineData("Pricing:LastMinuteDays", "-1")]
    [InlineData("Pricing:LastMinuteMultiplier", "0.95")]
    public void PricingSettings_InvalidConfiguration_FailsOnStartup(string configKey, string configValue)
    {
        using var invalidFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [configKey] = configValue
                });
            });
        });

        Assert.Throws<OptionsValidationException>(() => invalidFactory.CreateClient());
    }
}
