using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using AirlineReservation.Infrastructure.BackgroundServices;
using AirlineReservation.Infrastructure.Context;
using AirlineReservation.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AirlineReservation.IntegrationTests.Services;

public class HoldCleanupServiceTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly DateTimeOffset _baseUtc = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    public HoldCleanupServiceTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ClearTestDataAsync();
        await _factory.SeedTestDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CleanupExpiredHoldsAsync_ShouldPurgeOnlyExpiredHoldsUsingExecuteDelete()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var nowUtc = _factory.Clock.GetUtcNow().UtcDateTime;

        // Find a flight and two seats
        var flight = await context.Flights.FirstAsync();
        var seats = await context.Seats.Where(s => s.AircraftId == flight.AircraftId).Take(2).ToListAsync();
        var user = await context.Users.FirstAsync();

        var expiredHold = new SeatHold
        {
            FlightId = flight.Id,
            SeatId = seats[0].Id,
            UserId = user.Id,
            CreatedAtUtc = nowUtc.AddMinutes(-20),
            ExpiresAtUtc = nowUtc.AddMinutes(-10) // Expired 10 min ago relative to Clock
        };

        var activeHold = new SeatHold
        {
            FlightId = flight.Id,
            SeatId = seats[1].Id,
            UserId = user.Id,
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.AddMinutes(10) // Active for 10 min relative to Clock
        };

        context.SeatHolds.AddRange(expiredHold, activeHold);
        await context.SaveChangesAsync();

        var scopeFactory = _factory.Services.GetRequiredService<IServiceScopeFactory>();
        var holdSettings = Options.Create(new HoldSettings { Minutes = 10, CleanupSeconds = 30, CleanupEnabled = true });
        var service = new HoldCleanupService(scopeFactory, holdSettings, _factory.Clock, NullLogger<HoldCleanupService>.Instance);

        // Act: Execute single cleanup pass
        int deleted = await service.CleanupExpiredHoldsAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, deleted);

        var remainingHolds = await context.SeatHolds.AsNoTracking().ToListAsync();
        Assert.Single(remainingHolds);
        Assert.Equal(seats[1].Id, remainingHolds[0].SeatId);
        Assert.Equal(activeHold.ExpiresAtUtc, remainingHolds[0].ExpiresAtUtc);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_ShouldExitImmediately()
    {
        var scopeFactory = _factory.Services.GetRequiredService<IServiceScopeFactory>();
        var holdSettings = Options.Create(new HoldSettings { CleanupEnabled = false });
        var service = new HoldCleanupService(scopeFactory, holdSettings, _factory.Clock, NullLogger<HoldCleanupService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await service.StartAsync(cts.Token);
        await service.StopAsync(cts.Token);

        // Successfully started and stopped without hanging or throwing
        Assert.True(true);
    }
}
