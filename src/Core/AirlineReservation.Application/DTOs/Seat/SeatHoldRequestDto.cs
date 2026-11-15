namespace AirlineReservation.Application.DTOs.Seat;

/// <summary>
/// Request payload for holding one or more seats on a scheduled flight.
/// </summary>
public sealed record SeatHoldRequestDto
{
    public IReadOnlyList<int> SeatIds { get; init; } = Array.Empty<int>();

    public SeatHoldRequestDto() { }

    public SeatHoldRequestDto(IReadOnlyList<int> seatIds)
    {
        SeatIds = seatIds ?? Array.Empty<int>();
    }
}
