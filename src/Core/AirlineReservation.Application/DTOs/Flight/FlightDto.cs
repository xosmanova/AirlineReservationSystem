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
    public string AircraftModel { get; set; } = string.Empty;
    public int TotalSeats { get; set; }
    public int AvailableSeatsCount { get; set; }
    public int DepartureAirportId { get; set; }
    public AirportDto? DepartureAirport { get; set; }
    public int ArrivalAirportId { get; set; }
    public AirportDto? ArrivalAirport { get; set; }
    public DateTime DepartureTimeUtc { get; set; }
    public DateTime ArrivalTimeUtc { get; set; }
    public int Stops { get; set; }
    public decimal BasePrice { get; set; }
    public FlightStatus Status { get; set; }

    // Computed properties for frontend and mobile convenience
    public TimeSpan Duration => ArrivalTimeUtc > DepartureTimeUtc ? ArrivalTimeUtc - DepartureTimeUtc : TimeSpan.Zero;
    public int DurationMinutes => (int)Duration.TotalMinutes;
}
