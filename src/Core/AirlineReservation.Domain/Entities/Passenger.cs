using AirlineReservation.Domain.Common;

namespace AirlineReservation.Domain.Entities;

public class Passenger : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PassportNumber { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty; // Male, Female, Other

    // Navigation properties
    public ICollection<Ticket> Tickets { get; set; } = [];
}
