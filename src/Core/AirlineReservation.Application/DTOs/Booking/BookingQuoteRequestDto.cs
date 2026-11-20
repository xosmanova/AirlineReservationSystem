namespace AirlineReservation.Application.DTOs.Booking;

/// <summary>
/// Request payload for calculating an informational seat price quote before booking checkout.
/// </summary>
public sealed record BookingQuoteRequestDto(
    int FlightId,
    IReadOnlyList<int>? SeatIds);
