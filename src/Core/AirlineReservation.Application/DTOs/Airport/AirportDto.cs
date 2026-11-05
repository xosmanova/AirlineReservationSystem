namespace AirlineReservation.Application.DTOs.Airport;

public class AirportDto
{
    public int Id { get; set; }
    public string IataCode { get; set; } = string.Empty; // e.g. GYD, IST
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
}
