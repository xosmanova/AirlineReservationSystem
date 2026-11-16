using AirlineReservation.Application.DTOs.Booking;
using AirlineReservation.Domain.Entities;

namespace AirlineReservation.Application.Features.Bookings;

/// <summary>
/// Mapping helper converting Booking entity graph into BookingDetailsDto.
/// </summary>
public static class BookingMappingExtensions
{
    public static BookingDetailsDto ToDetailsDto(this Booking booking)
    {
        ArgumentNullException.ThrowIfNull(booking);

        var firstTicket = booking.Tickets.FirstOrDefault();
        var flight = firstTicket?.Flight;

        return new BookingDetailsDto
        {
            Id = booking.Id,
            PnrCode = booking.PnrCode,
            UserId = booking.UserId,
            TotalAmount = booking.TotalAmount,
            Status = booking.Status.ToString(),
            ContactEmail = booking.ContactEmail,
            ContactPhone = booking.ContactPhone,
            CreatedAtUtc = booking.CreatedAt,
            RefundAmount = booking.RefundAmount,
            CancelledAtUtc = booking.CancelledAtUtc,

            FlightId = flight?.Id ?? 0,
            FlightNumber = flight?.FlightNumber ?? string.Empty,
            AirlineName = flight?.Airline?.Name ?? string.Empty,
            DepartureAirportCode = flight?.DepartureAirport?.IataCode ?? string.Empty,
            DepartureAirportName = flight?.DepartureAirport?.Name ?? string.Empty,
            DepartureCity = flight?.DepartureAirport?.City ?? string.Empty,
            ArrivalAirportCode = flight?.ArrivalAirport?.IataCode ?? string.Empty,
            ArrivalAirportName = flight?.ArrivalAirport?.Name ?? string.Empty,
            ArrivalCity = flight?.ArrivalAirport?.City ?? string.Empty,
            DepartureTimeUtc = flight?.DepartureTimeUtc ?? DateTime.MinValue,
            ArrivalTimeUtc = flight?.ArrivalTimeUtc ?? DateTime.MinValue,
            FlightStatus = flight?.Status.ToString() ?? string.Empty,

            Tickets = booking.Tickets.Select(t => new BookingTicketDto
            {
                TicketId = t.Id,
                TicketNumber = t.TicketNumber,
                SeatId = t.SeatId,
                SeatNumber = t.Seat?.SeatNumber ?? string.Empty,
                SeatClass = t.Seat?.SeatClass.ToString() ?? string.Empty,
                Price = t.Price,
                IsCancelled = t.IsCancelled,
                PassengerFirstName = t.Passenger?.FirstName ?? string.Empty,
                PassengerLastName = t.Passenger?.LastName ?? string.Empty,
                PassportNumber = t.Passenger?.PassportNumber ?? string.Empty,
                DateOfBirth = t.Passenger?.DateOfBirth ?? DateTime.MinValue
            }).ToList(),

            Payment = booking.Payment != null
                ? new BookingPaymentDto
                {
                    PaymentId = booking.Payment.Id,
                    Amount = booking.Payment.Amount,
                    PaymentDate = booking.Payment.PaymentDate,
                    PaymentMethod = booking.Payment.PaymentMethod,
                    TransactionReference = booking.Payment.TransactionReference,
                    Status = booking.Payment.Status.ToString()
                }
                : null
        };
    }
}
