namespace AirlineReservation.Application.DTOs.Seat;

/// <summary>
/// Comprehensive flight seat map data transfer object containing structured rows and SVG markup.
/// </summary>
public class SeatMapDto
{
    public int FlightId { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public string AirlineName { get; set; } = string.Empty;
    public string AircraftModel { get; set; } = string.Empty;
    public int TotalSeats { get; set; }
    public int AvailableSeatsCount { get; set; }
    public int OccupiedSeatsCount { get; set; }
    public decimal BasePrice { get; set; }
    public IReadOnlyList<SeatRowDto> Rows { get; set; } = [];
    public string SvgMap { get; set; } = string.Empty;
}
