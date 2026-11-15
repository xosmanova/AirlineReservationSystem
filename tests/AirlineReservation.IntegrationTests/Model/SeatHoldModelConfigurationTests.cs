using AirlineReservation.Domain.Entities;
using AirlineReservation.Infrastructure.Context;
using AirlineReservation.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AirlineReservation.IntegrationTests.Model;

public class SeatHoldModelConfigurationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SeatHoldModelConfigurationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void SeatHold_Configuration_HasUnfilteredUniqueIndex_AndRestrictForeignKeys()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entityType = context.Model.FindEntityType(typeof(SeatHold));

        Assert.NotNull(entityType);

        // Primary key is Id
        var pk = entityType.FindPrimaryKey();
        Assert.NotNull(pk);
        Assert.Equal(nameof(SeatHold.Id), Assert.Single(pk.Properties).Name);

        // Unfiltered unique index on (FlightId, SeatId)
        var uniqueIndex = entityType.GetIndexes()
            .SingleOrDefault(i => i.Properties.Count == 2
                                  && i.Properties[0].Name == nameof(SeatHold.FlightId)
                                  && i.Properties[1].Name == nameof(SeatHold.SeatId));
        Assert.NotNull(uniqueIndex);
        Assert.True(uniqueIndex.IsUnique);
        Assert.Null(uniqueIndex.GetFilter()); // Must be unfiltered!

        // Composite index on (FlightId, UserId)
        var compositeIndex = entityType.GetIndexes()
            .SingleOrDefault(i => i.Properties.Count == 2
                                  && i.Properties[0].Name == nameof(SeatHold.FlightId)
                                  && i.Properties[1].Name == nameof(SeatHold.UserId));
        Assert.NotNull(compositeIndex);

        // Index on ExpiresAtUtc
        var expiryIndex = entityType.GetIndexes()
            .SingleOrDefault(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(SeatHold.ExpiresAtUtc));
        Assert.NotNull(expiryIndex);

        // All foreign keys are Restrict
        var fks = entityType.GetForeignKeys().ToList();
        Assert.Equal(3, fks.Count);
        Assert.All(fks, fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));

        // Soft delete query filter must NOT be present on SeatHold
        Assert.Empty(entityType.GetDeclaredQueryFilters());
    }
}
