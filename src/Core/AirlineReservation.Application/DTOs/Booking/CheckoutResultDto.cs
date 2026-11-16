namespace AirlineReservation.Application.DTOs.Booking;

/// <summary>
/// Booking confirmation data returned upon successful checkout.
/// </summary>
public class CheckoutResultDto
{
    public string PnrCode { get; set; } = string.Empty;
    public int BookingId { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
