namespace AirlineReservation.Application.DTOs.Booking;

/// <summary>
/// Primary contact details for booking confirmation and itinerary updates.
/// </summary>
public class CheckoutContactDto
{
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}
