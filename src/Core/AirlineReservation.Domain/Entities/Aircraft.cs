using AirlineReservation.Domain.Common;

namespace AirlineReservation.Domain.Entities;

public class Aircraft : BaseEntity
{
    public string Model { get; set; } = string.Empty;      // e.g. Airbus A320, Boeing 737-800
    public string TailNumber { get; set; } = string.Empty; // e.g. 4K-AZ01
    public int TotalSeats { get; set; }

    // Foreign Keys
    public int AirlineId { get; set; }
    public Airline Airline { get; set; } = null!;

    // Navigation properties
    public ICollection<Seat> Seats { get; set; } = [];
    public ICollection<Flight> Flights { get; set; } = [];
}
