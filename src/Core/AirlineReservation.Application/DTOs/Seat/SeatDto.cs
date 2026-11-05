using AirlineReservation.Domain.Enums;

namespace AirlineReservation.Application.DTOs.Seat;

public class SeatDto
{
    public int Id { get; set; }
    public int AircraftId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public int RowNumber { get; set; }
    public string ColumnLetter { get; set; } = string.Empty;
    public SeatClass SeatClass { get; set; }
    public decimal BasePriceMultiplier { get; set; }
    public bool IsWindow { get; set; }
    public bool IsAisle { get; set; }
    public bool IsExitRow { get; set; }
    public bool IsOccupied { get; set; }
}
