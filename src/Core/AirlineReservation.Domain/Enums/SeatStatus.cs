namespace AirlineReservation.Domain.Enums;

/// <summary>
/// Availability status of a seat on a specific flight.
/// Held and Blocked states become active in Day 14 (SeatHold) and Day 31 (FlightSeatBlock).
/// </summary>
public enum SeatStatus
{
    Available = 0,
    Occupied = 1,
    Held = 2,
    Blocked = 3
}
