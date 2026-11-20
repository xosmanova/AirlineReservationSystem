using Microsoft.AspNetCore.Identity;

namespace AirlineReservation.Infrastructure.Identity.Entities;

/// <summary>
/// Custom application role deriving from ASP.NET Core IdentityRole with a Guid primary key.
/// </summary>
public class AppRole : IdentityRole<Guid>
{
    public AppRole() : base()
    {
    }

    public AppRole(string roleName) : base(roleName)
    {
    }
}
