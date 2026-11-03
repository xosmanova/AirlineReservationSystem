using AirlineReservation.Domain.Common;

namespace AirlineReservation.Domain.Entities;

public class Airline : BaseEntity
{
    public string Code { get; set; } = string.Empty;     // e.g. J2, TK, EK
    public string Name { get; set; } = string.Empty;     // Azerbaijan Airlines, Turkish Airlines
    public string? LogoUrl { get; set; }

    // Navigation properties
    public ICollection<Aircraft> Aircrafts { get; set; } = [];
    public ICollection<Flight> Flights { get; set; } = [];
}
