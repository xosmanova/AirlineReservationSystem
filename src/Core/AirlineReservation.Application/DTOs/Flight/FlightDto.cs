using AirlineReservation.Application.DTOs.Airline;
using AirlineReservation.Application.DTOs.Airport;
using AirlineReservation.Domain.Enums;

namespace AirlineReservation.Application.DTOs.Flight;

public class FlightDto
{
    public int Id { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public int AirlineId { get; set; }
    public AirlineDto? Airline { get; set; }
    public int AircraftId { get; set; }
    public int DepartureAirportId { get; set; }
    public AirportDto? DepartureAirport { get; set; }
    public int ArrivalAirportId { get; set; }
    public AirportDto? ArrivalAirport { get; set; }
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public decimal BasePrice { get; set; }
    public FlightStatus Status { get; set; }
    public TimeSpan Duration { get; set; }
}
