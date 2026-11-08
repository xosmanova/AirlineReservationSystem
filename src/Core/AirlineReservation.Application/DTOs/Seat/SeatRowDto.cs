using AirlineReservation.Domain.Enums;

namespace AirlineReservation.Application.DTOs.Seat;

/// <summary>
/// Represents a single cabin row containing seats.
/// </summary>
public class SeatRowDto
{
    public int RowNumber { get; set; }
    public SeatClass SeatClass { get; set; }
    public bool IsExitRow { get; set; }
    public IReadOnlyList<FlightSeatDto> Seats { get; set; } = [];
}
