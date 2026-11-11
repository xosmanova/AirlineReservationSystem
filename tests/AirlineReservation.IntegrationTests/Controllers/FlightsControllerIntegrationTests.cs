using AirlineReservation.Application.DTOs.Seat;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Flight;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using AirlineReservation.Infrastructure.Context;
using AirlineReservation.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AirlineReservation.IntegrationTests.Controllers;

public class FlightsControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public FlightsControllerIntegrationTests(CustomWebApplicationFactory factory)
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
    public async Task Flight_WithOneFreeSeat_IsExcluded_WhenSearchingForTwoPassengers()
    {
        int depId = _factory.TestDepartureAirportId;
        int arrId = _factory.TestArrivalAirportId;
        int flight1Id = _factory.TestFlight1Id;

        // Act: Search for 2 passengers on 2026-11-10 (Baku date)
        var response2p = await _client.GetAsync(
            $"/api/v1/flights/search?departureAirportId={depId}&arrivalAirportId={arrId}&departureDate=2026-11-10&passengersCount=2&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response2p.StatusCode);
        var result2p = await response2p.Content.ReadFromJsonAsync<ApiResponse<PagedResult<FlightDto>>>(JsonOptions);
        Assert.NotNull(result2p);
        Assert.NotNull(result2p.Data);

        // Flight 1 has only 1 available seat, so it must be excluded in SQL for 2 passengers
        Assert.DoesNotContain(result2p.Data.Items, f => f.Id == flight1Id);

        // Act: Search for 1 passenger
        var response1p = await _client.GetAsync(
            $"/api/v1/flights/search?departureAirportId={depId}&arrivalAirportId={arrId}&departureDate=2026-11-10&passengersCount=1&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response1p.StatusCode);
        var result1p = await response1p.Content.ReadFromJsonAsync<ApiResponse<PagedResult<FlightDto>>>(JsonOptions);
        Assert.NotNull(result1p);
        Assert.NotNull(result1p.Data);

        // Flight 1 must be present for 1 passenger with AvailableSeatsCount = 1
        var flight1 = Assert.Single(result1p.Data.Items, f => f.Id == flight1Id);
        Assert.Equal(1, flight1.AvailableSeatsCount);
    }

    [Fact]
    public async Task Pagination_And_TotalCount_AreConsistentAcrossPages()
    {
        int depId = _factory.TestDepartureAirportId;
        int arrId = _factory.TestArrivalAirportId;

        // Arrange: 14 eligible flights exist in test database for 1 passenger
        // Act: Page 1 with pageSize = 5
        var resPage1 = await _client.GetAsync(
            $"/api/v1/flights/search?departureAirportId={depId}&arrivalAirportId={arrId}&departureDate=2026-11-10&passengersCount=1&page=1&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, resPage1.StatusCode);
        var page1 = await resPage1.Content.ReadFromJsonAsync<ApiResponse<PagedResult<FlightDto>>>(JsonOptions);
        Assert.NotNull(page1?.Data);
        Assert.Equal(14, page1.Data.TotalCount);
        Assert.Equal(5, page1.Data.Items.Count);
        Assert.Equal(1, page1.Data.Page);
        Assert.Equal(5, page1.Data.PageSize);
        Assert.True(page1.Data.HasNextPage);
        Assert.False(page1.Data.HasPreviousPage);

        // Act: Page 2 with pageSize = 5
        var resPage2 = await _client.GetAsync(
            $"/api/v1/flights/search?departureAirportId={depId}&arrivalAirportId={arrId}&departureDate=2026-11-10&passengersCount=1&page=2&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, resPage2.StatusCode);
        var page2 = await resPage2.Content.ReadFromJsonAsync<ApiResponse<PagedResult<FlightDto>>>(JsonOptions);
        Assert.NotNull(page2?.Data);
        Assert.Equal(14, page2.Data.TotalCount);
        Assert.Equal(5, page2.Data.Items.Count);
        Assert.Equal(2, page2.Data.Page);
        Assert.True(page2.Data.HasNextPage);
        Assert.True(page2.Data.HasPreviousPage);

        // Act: Page 3 with pageSize = 5 (remaining 4 flights)
        var resPage3 = await _client.GetAsync(
            $"/api/v1/flights/search?departureAirportId={depId}&arrivalAirportId={arrId}&departureDate=2026-11-10&passengersCount=1&page=3&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, resPage3.StatusCode);
        var page3 = await resPage3.Content.ReadFromJsonAsync<ApiResponse<PagedResult<FlightDto>>>(JsonOptions);
        Assert.NotNull(page3?.Data);
        Assert.Equal(14, page3.Data.TotalCount);
        Assert.Equal(4, page3.Data.Items.Count);
        Assert.Equal(3, page3.Data.Page);
        Assert.False(page3.Data.HasNextPage);
        Assert.True(page3.Data.HasPreviousPage);
    }

    [Fact]
    public async Task DelayedFlight_IsReturnedInSearchResults()
    {
        int depId = _factory.TestDepartureAirportId;
        int arrId = _factory.TestArrivalAirportId;
        int delayedFlightId = _factory.TestFlight2DelayedId;

        // Act: Search for flights (Flight 2 is Delayed)
        var response = await _client.GetAsync(
            $"/api/v1/flights/search?departureAirportId={depId}&arrivalAirportId={arrId}&departureDate=2026-11-10&passengersCount=1&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<FlightDto>>>(JsonOptions);
        Assert.NotNull(result?.Data);

        var delayedFlight = Assert.Single(result.Data.Items, f => f.Id == delayedFlightId);
        Assert.Equal(FlightStatus.Delayed, delayedFlight.Status);
    }

    [Fact]
    public async Task CancelledFlight_IsNotReturnedInSearchResults()
    {
        int depId = _factory.TestDepartureAirportId;
        int arrId = _factory.TestArrivalAirportId;
        int cancelledFlightId = _factory.TestFlight3CancelledId;

        // Act: Search for flights (Flight 3 is Cancelled)
        var response = await _client.GetAsync(
            $"/api/v1/flights/search?departureAirportId={depId}&arrivalAirportId={arrId}&departureDate=2026-11-10&passengersCount=1&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<FlightDto>>>(JsonOptions);
        Assert.NotNull(result?.Data);

        Assert.DoesNotContain(result.Data.Items, f => f.Id == cancelledFlightId);
    }

    [Fact]
    public async Task SearchAvailableSeatsCount_Matches_SeatMapAvailableSeatsCount_Exactly()
    {
        int depId = _factory.TestDepartureAirportId;
        int arrId = _factory.TestArrivalAirportId;
        int flight1Id = _factory.TestFlight1Id;

        // 1. Search endpoint
        var searchResp = await _client.GetAsync(
            $"/api/v1/flights/search?departureAirportId={depId}&arrivalAirportId={arrId}&departureDate=2026-11-10&passengersCount=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, searchResp.StatusCode);
        var searchResult = await searchResp.Content.ReadFromJsonAsync<ApiResponse<PagedResult<FlightDto>>>(JsonOptions);
        var flightInSearch = Assert.Single(searchResult!.Data!.Items, f => f.Id == flight1Id);

        // 2. SeatMap endpoint
        var seatMapResp = await _client.GetAsync($"/api/v1/flights/{flight1Id}/seatmap");
        Assert.Equal(HttpStatusCode.OK, seatMapResp.StatusCode);
        var seatMapResult = await seatMapResp.Content.ReadFromJsonAsync<ApiResponse<SeatMapDto>>(JsonOptions);
        Assert.NotNull(seatMapResult?.Data);

        // 3. Exact equality between search and seat map
        Assert.Equal(flightInSearch.AvailableSeatsCount, seatMapResult.Data.AvailableSeatsCount);
        int countedFreeSeatsInRows = seatMapResult.Data.Rows.SelectMany(r => r.Seats).Count(s => s.Status == SeatStatus.Available);
        Assert.Equal(flightInSearch.AvailableSeatsCount, countedFreeSeatsInRows);
    }

    [Fact]
    public async Task SeatMap_And_FlightSearch_SerializeEnumsAsStrings_AndOmitSvgMap()
    {
        int depId = _factory.TestDepartureAirportId;
        int arrId = _factory.TestArrivalAirportId;
        int flight1Id = _factory.TestFlight1Id;

        var seatMapResp = await _client.GetAsync($"/api/v1/flights/{flight1Id}/seatmap");
        Assert.Equal(HttpStatusCode.OK, seatMapResp.StatusCode);
        string seatMapJson = await seatMapResp.Content.ReadAsStringAsync();

        Assert.Contains("\"status\":\"Available\"", seatMapJson);
        Assert.Contains("\"status\":\"Occupied\"", seatMapJson);
        Assert.Contains("\"seatClass\":\"Economy\"", seatMapJson);
        Assert.Contains("\"heldByMe\":false", seatMapJson);
        Assert.DoesNotContain("\"svgMap\"", seatMapJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"isOccupied\"", seatMapJson, StringComparison.OrdinalIgnoreCase);

        var searchResp = await _client.GetAsync(
            $"/api/v1/flights/search?departureAirportId={depId}&arrivalAirportId={arrId}&departureDate=2026-11-10&passengersCount=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, searchResp.StatusCode);
        string searchJson = await searchResp.Content.ReadAsStringAsync();

        Assert.Contains("\"status\":\"Scheduled\"", searchJson);
        Assert.Contains("\"status\":\"Delayed\"", searchJson);

        var svgResp = await _client.GetAsync($"/api/v1/flights/{flight1Id}/seatmap/svg");
        Assert.Equal(HttpStatusCode.OK, svgResp.StatusCode);
        Assert.Equal("image/svg+xml", svgResp.Content.Headers.ContentType?.MediaType);
        string svg = await svgResp.Content.ReadAsStringAsync();
        Assert.StartsWith("<svg", svg);
    }

    [Fact]
    public async Task SeatMap_ProjectionQuery_ExcludesCancelledTicketsAndCancelledBookings_InSql()
    {
        int flight1Id = _factory.TestFlight1Id;

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ownerUserId = await context.Users.Select(u => u.Id).FirstAsync();
            var seat1B = await context.Seats.FirstAsync(s => s.SeatNumber == "1B");
            var passengerId = await context.Passengers.Select(p => p.Id).FirstAsync();

            var cancelledBooking = new Booking
            {
                UserId = ownerUserId,
                PnrCode = "CNCL01",
                ContactEmail = "cancel@airline.test",
                ContactPhone = "+994501112233",
                Status = BookingStatus.Cancelled,
                TotalAmount = 150m
            };
            context.Bookings.Add(cancelledBooking);

            var confirmedBooking = new Booking
            {
                UserId = ownerUserId,
                PnrCode = "CONF02",
                ContactEmail = "conf@airline.test",
                ContactPhone = "+994501112244",
                Status = BookingStatus.Confirmed,
                TotalAmount = 150m
            };
            context.Bookings.Add(confirmedBooking);
            await context.SaveChangesAsync();

            // Ticket from a Cancelled booking + Cancelled ticket from a Confirmed booking on Seat 1B
            context.Tickets.AddRange(
                new Ticket
                {
                    TicketNumber = "ET-CNCL-001",
                    FlightId = flight1Id,
                    BookingId = cancelledBooking.Id,
                    PassengerId = passengerId,
                    SeatId = seat1B.Id,
                    Price = 150m,
                    IsCancelled = false
                },
                new Ticket
                {
                    TicketNumber = "ET-CNCL-002",
                    FlightId = flight1Id,
                    BookingId = confirmedBooking.Id,
                    PassengerId = passengerId,
                    SeatId = seat1B.Id,
                    Price = 150m,
                    IsCancelled = true
                });
            await context.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/v1/flights/{flight1Id}/seatmap");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var seatMap = (await response.Content.ReadFromJsonAsync<ApiResponse<SeatMapDto>>(JsonOptions))!.Data!;
        var seat1A = seatMap.Rows.SelectMany(r => r.Seats).Single(s => s.SeatNumber == "1A");
        var seat1BResult = seatMap.Rows.SelectMany(r => r.Seats).Single(s => s.SeatNumber == "1B");

        Assert.Equal(SeatStatus.Occupied, seat1A.Status);
        Assert.Equal(SeatStatus.Available, seat1BResult.Status);
        Assert.Equal(1, seatMap.OccupiedSeatsCount);
        Assert.Equal(1, seatMap.AvailableSeatsCount);
    }
}
