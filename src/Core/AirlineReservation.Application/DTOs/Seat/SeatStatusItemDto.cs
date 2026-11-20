using AirlineReservation.Domain.Enums;

namespace AirlineReservation.Application.DTOs.Seat;

/// <summary>
/// Lightweight seat status item for polling and delta updates.
/// </summary>
public sealed record SeatStatusItemDto(
    int SeatId,
    SeatStatus Status,
    bool HeldByMe);
