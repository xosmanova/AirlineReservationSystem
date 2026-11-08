using AirlineReservation.Domain.Enums;

namespace AirlineReservation.Application.DTOs.Seat;

/// <summary>
/// Detailed seat representation for a specific flight, including availability and dynamic price.
/// </summary>
public class FlightSeatDto
{
    public int Id { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public int RowNumber { get; set; }
    public string ColumnLetter { get; set; } = string.Empty;
    public SeatClass SeatClass { get; set; }
    public decimal BasePriceMultiplier { get; set; } = 1.0m;
    public decimal Price { get; set; }
    public bool IsOccupied { get; set; }
    public bool IsWindow { get; set; }
    public bool IsAisle { get; set; }
    public bool IsExitRow { get; set; }
}
