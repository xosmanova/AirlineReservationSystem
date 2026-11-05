namespace AirlineReservation.Application.DTOs.Passenger;

/// <summary>
/// Data transfer object for passenger information during booking creation.
/// </summary>
public class PassengerCreateDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PassportNumber { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public int SeatId { get; set; }
}
