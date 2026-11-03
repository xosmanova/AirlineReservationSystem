using AirlineReservation.Domain.Common;
using AirlineReservation.Domain.Enums;

namespace AirlineReservation.Domain.Entities;

public class Payment : BaseEntity
{
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public string PaymentMethod { get; set; } = "CreditCard"; // CreditCard, DebitCard, MockGateway
    public string TransactionReference { get; set; } = string.Empty;

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
}
