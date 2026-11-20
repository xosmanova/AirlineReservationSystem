namespace AirlineReservation.Application.DTOs.Airport;

/// <summary>
/// Data transfer object representing airport details for flight lookups and autocomplete searches.
/// </summary>
public class AirportDto
{
    public int Id { get; set; }
    public string IataCode { get; set; } = string.Empty; // e.g. GYD, IST
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated alternative names, localized city spellings, or aliases for search (e.g. Baku,Bakı,Baki,Баку).
    /// </summary>
    public string? Aliases { get; set; }
}
