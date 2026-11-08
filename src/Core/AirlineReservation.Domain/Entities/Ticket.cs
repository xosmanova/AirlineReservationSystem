using AirlineReservation.Domain.Common;

namespace AirlineReservation.Domain.Entities;

public class Ticket : BaseEntity
{
    public string TicketNumber { get; set; } = string.Empty; // e.g. "ET-2026-0001"

    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public int FlightId { get; set; }
    public Flight Flight { get; set; } = null!;

    public int PassengerId { get; set; }
    public Passenger Passenger { get; set; } = null!;

    public int SeatId { get; set; }
    public Seat Seat { get; set; } = null!;

    public decimal Price { get; set; }

    public bool IsCancelled { get; set; } = false;
    public DateTime? CancelledAtUtc { get; set; }
}
