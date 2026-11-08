using AirlineReservation.Domain.Common;
using AirlineReservation.Domain.Enums;

namespace AirlineReservation.Domain.Entities;

public class Flight : BaseEntity
{
    public string FlightNumber { get; set; } = string.Empty; // e.g. J2-075, TK-338

    public int AirlineId { get; set; }
    public Airline Airline { get; set; } = null!;

    public int AircraftId { get; set; }
    public Aircraft Aircraft { get; set; } = null!;

    public int DepartureAirportId { get; set; }
    public Airport DepartureAirport { get; set; } = null!;

    public int ArrivalAirportId { get; set; }
    public Airport ArrivalAirport { get; set; } = null!;

    public DateTime DepartureTimeUtc { get; set; }
    public DateTime ArrivalTimeUtc { get; set; }

    // Number of stops (0 = Direct, 1 = 1 stop transit)
    public int Stops { get; set; } = 0;

    public decimal BasePrice { get; set; }
    public FlightStatus Status { get; set; } = FlightStatus.Scheduled;

    // Computed / Helper property
    public TimeSpan Duration => ArrivalTimeUtc - DepartureTimeUtc;

    // Navigation properties
    public ICollection<Ticket> Tickets { get; set; } = [];
}
