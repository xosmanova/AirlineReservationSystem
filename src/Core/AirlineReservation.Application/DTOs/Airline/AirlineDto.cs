namespace AirlineReservation.Application.DTOs.Airline;

public class AirlineDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty; // IATA (e.g. J2, TK)
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
}
