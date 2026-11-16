namespace AirlineReservation.Application.DTOs.Booking;

/// <summary>
/// Preview calculations for cancelling a booking, including refund and penalty amounts.
/// </summary>
public class CancelBookingPreviewDto
{
    public int BookingId { get; set; }
    public string PnrCode { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal RefundPercentage { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal PenaltyAmount { get; set; }
    public bool CanCancel { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime DepartureTimeUtc { get; set; }
}
