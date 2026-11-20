namespace AirlineReservation.Application.DTOs.Booking;

/// <summary>
/// Comprehensive booking details representation returned for PNR lookups and user trip itineraries.
/// </summary>
public class BookingDetailsDto
{
    public int Id { get; set; }
    public string PnrCode { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public decimal? RefundAmount { get; set; }
    public DateTime? CancelledAtUtc { get; set; }

    // Flight Details
    public int FlightId { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public string AirlineName { get; set; } = string.Empty;
    public string DepartureAirportCode { get; set; } = string.Empty;
    public string DepartureAirportName { get; set; } = string.Empty;
    public string DepartureCity { get; set; } = string.Empty;
    public string ArrivalAirportCode { get; set; } = string.Empty;
    public string ArrivalAirportName { get; set; } = string.Empty;
    public string ArrivalCity { get; set; } = string.Empty;
    public DateTime DepartureTimeUtc { get; set; }
    public DateTime ArrivalTimeUtc { get; set; }
    public string FlightStatus { get; set; } = string.Empty;

    // Tickets & Passengers
    public IReadOnlyList<BookingTicketDto> Tickets { get; set; } = [];

    // Payment Information
    public BookingPaymentDto? Payment { get; set; }
}

public class BookingTicketDto
{
    public int TicketId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public int SeatId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public string SeatClass { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsCancelled { get; set; }
    public string PassengerFirstName { get; set; } = string.Empty;
    public string PassengerLastName { get; set; } = string.Empty;
    public string PassportNumber { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
}

public class BookingPaymentDto
{
    public int PaymentId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string TransactionReference { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
