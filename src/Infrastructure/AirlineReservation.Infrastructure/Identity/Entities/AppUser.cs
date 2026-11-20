using Microsoft.AspNetCore.Identity;

namespace AirlineReservation.Infrastructure.Identity.Entities;

/// <summary>
/// Custom application user deriving from ASP.NET Core IdentityUser with a Guid primary key.
/// Lives strictly in the Infrastructure layer to maintain Clean Architecture isolation.
/// </summary>
public class AppUser : IdentityUser<Guid>
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
