using System.Net;
using System.Net.Http.Json;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Airport;
using AirlineReservation.IntegrationTests.Fixtures;
using Xunit;

namespace AirlineReservation.IntegrationTests.Controllers;

public class AirportsControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AirportsControllerIntegrationTests(CustomWebApplicationFactory factory)
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

    [Theory]
    [InlineData("Bakı")]
    [InlineData("baku")]
    [InlineData("Баку")]
    [InlineData("GYD")]
    public async Task Search_WithDifferentBakuKeywordsAndAliases_ReturnsHeydarAliyevAirport(string keyword)
    {
        // Act: url-encode keyword to handle non-ASCII/Cyrillic properly
        var response = await _client.GetAsync($"/api/v1/airports?search={Uri.EscapeDataString(keyword)}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<AirportDto>>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Contains(result.Data, a => a.IataCode == "GYD");
    }

    [Fact]
    public async Task Search_WithIst_ReturnsBothIstanbulAirports()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/airports?search=ist");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<AirportDto>>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var iataCodes = result.Data.Select(a => a.IataCode).ToHashSet();
        Assert.Contains("IST", iataCodes);
        Assert.Contains("SAW", iataCodes);
    }

    [Fact]
    public async Task Search_WithSabiha_ReturnsSabihaGokcenAirport()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/airports?search=Sabiha");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<AirportDto>>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Contains(result.Data, a => a.IataCode == "SAW");
    }

    [Fact]
    public async Task Search_WithUnknownTerm_ReturnsEmptyListAnd200OK()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/airports?search=xyznonexistent999");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<AirportDto>>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task Search_WithSearchTermExceeding50Chars_Returns400BadRequest()
    {
        // Act
        var longSearch = new string('x', 51);
        var response = await _client.GetAsync($"/api/v1/airports?search={longSearch}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
