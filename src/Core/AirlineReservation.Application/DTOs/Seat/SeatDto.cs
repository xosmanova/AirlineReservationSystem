using AirlineReservation.Domain.Enums;

namespace AirlineReservation.Application.DTOs.Seat;

/// <summary>
/// Detailed seat representation for an aircraft layout or flight seat map,
/// including real-time availability status and server-calculated pricing.
/// </summary>
public class SeatDto
{
    public int Id { get; set; }
    public int AircraftId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public int RowNumber { get; set; }
    public string ColumnLetter { get; set; } = string.Empty;
    public SeatClass SeatClass { get; set; }
    public decimal BasePriceMultiplier { get; set; } = 1.0m;
    public decimal Price { get; set; }
    public SeatStatus Status { get; set; } = SeatStatus.Available;

    /// <summary>
    /// Indicates whether the seat is currently held by the authenticated user.
    /// Always false until persistent seat holds are implemented on Day 14.
    /// </summary>
    public bool HeldByMe { get; set; }

    public bool IsWindow { get; set; }
    public bool IsAisle { get; set; }
    public bool IsExitRow { get; set; }
}
