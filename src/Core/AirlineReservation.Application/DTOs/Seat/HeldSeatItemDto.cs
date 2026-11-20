namespace AirlineReservation.Application.DTOs.Seat;

/// <summary>
/// Individual seat hold item including seat number and remaining duration.
/// </summary>
public sealed record HeldSeatItemDto(
    int SeatId,
    string SeatNumber,
    DateTime ExpiresAtUtc,
    int RemainingSeconds);
