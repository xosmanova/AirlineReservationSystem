using AirlineReservation.Application.DTOs.Booking;
using AirlineReservation.Domain.Entities;

namespace AirlineReservation.Application.Tests.Architecture;

public class CleanArchitectureBoundaryTests
{
    [Fact]
    public void DomainAssembly_ShouldNotReference_IdentityOrInfrastructure()
    {
        var referencedAssemblies = typeof(Booking).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(referencedAssemblies, name => name.Contains("Identity", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedAssemblies, name => name.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedAssemblies, name => name.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApplicationAssembly_ShouldNotReference_IdentityOrInfrastructure()
    {
        var referencedAssemblies = typeof(BookingDto).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(referencedAssemblies, name => name.Contains("Identity", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedAssemblies, name => name.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BookingAndBookingDto_UserId_ShouldBeRequiredNonNullableGuid()
    {
        var domainUserIdProp = typeof(Booking).GetProperty(nameof(Booking.UserId));
        var dtoUserIdProp = typeof(BookingDto).GetProperty(nameof(BookingDto.UserId));

        Assert.NotNull(domainUserIdProp);
        Assert.NotNull(dtoUserIdProp);
        Assert.Equal(typeof(Guid), domainUserIdProp.PropertyType);
        Assert.Equal(typeof(Guid), dtoUserIdProp.PropertyType);
    }
}
