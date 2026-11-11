namespace AirlineReservation.Application.DTOs.Booking;

/// <summary>
/// Server-calculated itemized quote for selected seats on a flight.
/// </summary>
/// <remarks>
/// This quote is informational only and does not lock or reserve seats.
/// Checkout recalculates the authoritative payable price server-side.
/// </remarks>
public sealed record BookingQuoteDto
{
    public int FlightId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public DateTime DepartureTimeUtc { get; init; }
    public decimal FlightBasePrice { get; init; }
    public decimal TotalPrice { get; init; }
    public IReadOnlyList<BookingQuoteItemDto> Items { get; init; } = [];
}
