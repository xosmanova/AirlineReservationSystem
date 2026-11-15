namespace AirlineReservation.Application.DTOs.Seat;

/// <summary>
/// Result of an atomic seat hold operation on a flight.
/// </summary>
public sealed record SeatHoldResultDto(
    int FlightId,
    IReadOnlyList<HeldSeatItemDto> HeldSeats,
    DateTime ExpiresAtUtc,
    int RemainingSeconds);
