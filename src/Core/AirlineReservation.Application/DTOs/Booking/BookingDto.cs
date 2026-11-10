using AirlineReservation.Domain.Enums;

namespace AirlineReservation.Application.DTOs.Booking;

public class BookingDto
{
    public int Id { get; set; }
    public string PnrCode { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public BookingStatus Status { get; set; }
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
