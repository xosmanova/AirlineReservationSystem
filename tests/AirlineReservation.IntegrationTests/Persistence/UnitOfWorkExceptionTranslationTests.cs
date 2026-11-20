using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Infrastructure.Context;
using AirlineReservation.Infrastructure.Repositories;
using AirlineReservation.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AirlineReservation.IntegrationTests.Persistence;

public class UnitOfWorkExceptionTranslationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;

    public UnitOfWorkExceptionTranslationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseCreatedAsync();
        await _factory.ClearTestDataAsync();
        await _factory.SeedTestDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SaveChangesAsync_TranslatesSqlDuplicateUniqueIndex_ToUniqueConstraintViolationException()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var uow = new UnitOfWork(context);

        int flightId = _factory.TestFlight1Id;
        var seat1A = await context.Seats.FirstAsync(s => s.SeatNumber == "1A");
        var userId = await context.Users.Select(u => u.Id).FirstAsync();

        var hold1 = new SeatHold
        {
            FlightId = flightId,
            SeatId = seat1A.Id,
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
        };
        await context.SeatHolds.AddAsync(hold1);
        await uow.SaveChangesAsync();

        // Attempt to insert duplicate hold for the same (FlightId, SeatId) pair
        var hold2 = new SeatHold
        {
            FlightId = flightId,
            SeatId = seat1A.Id,
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
        };
        await context.SeatHolds.AddAsync(hold2);

        var ex = await Assert.ThrowsAsync<UniqueConstraintViolationException>(() => uow.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
        Assert.IsType<DbUpdateException>(ex.InnerException);
    }

    [Fact]
    public async Task SaveChangesAsync_PreservesDbUpdateException_OnForeignKeyViolation()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var uow = new UnitOfWork(context);

        var seat1A = await context.Seats.FirstAsync(s => s.SeatNumber == "1A");
        var userId = await context.Users.Select(u => u.Id).FirstAsync();

        // FlightId 999999 does not exist, violating FK_SeatHolds_Flights_FlightId (SQL error 547)
        var invalidFkHold = new SeatHold
        {
            FlightId = 999999,
            SeatId = seat1A.Id,
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
        };
        await context.SeatHolds.AddAsync(invalidFkHold);

        // Must NOT be translated to UniqueConstraintViolationException!
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => uow.SaveChangesAsync());
        Assert.IsNotType<UniqueConstraintViolationException>(ex);
    }
}
