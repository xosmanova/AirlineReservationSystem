using AirlineReservation.Domain.Common;

namespace AirlineReservation.Domain.Entities;

public class Airport : BaseEntity
{
    public string IataCode { get; set; } = string.Empty; // e.g. GYD, IST, JFK
    public string Name { get; set; } = string.Empty;     // Heydar Aliyev International Airport
    public string City { get; set; } = string.Empty;     // Baku
    public string Country { get; set; } = string.Empty;  // Azerbaijan
    public string TimeZone { get; set; } = string.Empty; // UTC+4

    // Navigation properties
    public ICollection<Flight> DepartingFlights { get; set; } = [];
    public ICollection<Flight> ArrivingFlights { get; set; } = [];
}
