using System.Linq;
using System.Net;
using System.Net.Http.Json;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Airline;
using AirlineReservation.IntegrationTests.Fixtures;
using Xunit;

namespace AirlineReservation.IntegrationTests.Controllers;

public class AirlinesControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AirlinesControllerIntegrationTests(CustomWebApplicationFactory factory)
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
    public async Task GetAll_ReturnsAllRegisteredAirlines_With200OK()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/airlines");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<AirlineDto>>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.Count >= 2);

        var codes = result.Data.Select(a => a.Code).ToHashSet();
        Assert.Contains("J2", codes);
        Assert.Contains("TK", codes);
    }
}
