namespace AirlineReservation.Application.DTOs.Seat;

/// <summary>
/// Lightweight seat map status delta containing non-available seats and generation timestamp.
/// </summary>
public sealed record SeatStatusSummaryDto(
    int FlightId,
    DateTime GeneratedAtUtc,
    IReadOnlyList<SeatStatusItemDto> Seats);
