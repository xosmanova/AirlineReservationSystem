namespace AirlineReservation.Application.DTOs.Booking;

/// <summary>
/// Passenger information provided during checkout.
/// </summary>
public class CheckoutPassengerDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PassportNumber { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public int SeatId { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
}
