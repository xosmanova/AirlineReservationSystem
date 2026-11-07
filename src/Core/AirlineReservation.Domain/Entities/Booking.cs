using AirlineReservation.Domain.Common;
using AirlineReservation.Domain.Enums;

namespace AirlineReservation.Domain.Entities;

public class Booking : BaseEntity
{
    public string PnrCode { get; set; } = string.Empty; // 6-character alphanumeric, e.g. "AZ78KD"
    public string? UserId { get; set; }                 // Optional linked identity user (null for guest checkout)

    public decimal TotalAmount { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;

    public DateTime? ExpiresAtUtc { get; set; }        // 10-minute temporary seat reservation expiry

    // Navigation properties
    public ICollection<Ticket> Tickets { get; set; } = [];
    public Payment? Payment { get; set; }
}
