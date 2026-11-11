using AirlineReservation.Domain.Enums;

namespace AirlineReservation.Application.DTOs.Booking;

/// <summary>
/// Server-calculated price breakdown for a single quoted seat.
/// </summary>
public sealed record BookingQuoteItemDto
{
    public int SeatId { get; init; }
    public string SeatNumber { get; init; } = string.Empty;
    public SeatClass SeatClass { get; init; }
    public bool IsWindow { get; init; }
    public bool IsAisle { get; init; }
    public bool IsExitRow { get; init; }
    public decimal BaseSeatPrice { get; init; }
    public decimal PositionFee { get; init; }
    public decimal DemandMultiplier { get; init; }
    public bool IsLastMinuteDemandApplied { get; init; }
    public decimal FinalPrice { get; init; }
}
