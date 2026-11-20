namespace AirlineReservation.Domain.Entities;

/// <summary>
/// Temporary database-backed seat reservation hold created during passenger seat selection.
/// Automatically expires and does not use soft deletion (physical delete only).
/// </summary>
public class SeatHold
{
    public int Id { get; set; }
    public int FlightId { get; set; }
    public Flight Flight { get; set; } = null!;
    public int SeatId { get; set; }
    public Seat Seat { get; set; } = null!;
    public Guid UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
