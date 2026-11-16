using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Booking;
using MediatR;

namespace AirlineReservation.Application.Features.Bookings.Commands.Checkout;

/// <summary>
/// Command to complete flight checkout and purchase reserved tickets.
/// Note: UserId is NOT part of the request body (extracted securely from JWT claims in the API controller).
/// Overrides ToString() to guarantee sensitive card numbers and CVVs are never leaked in logs.
/// </summary>
public class CheckoutCommand : IRequest<ApiResponse<CheckoutResultDto>>
{
    public int FlightId { get; set; }
    public List<CheckoutPassengerDto> Passengers { get; set; } = [];
    public CheckoutContactDto Contact { get; set; } = new();
    public CardPaymentDto Card { get; set; } = new();

    /// <summary>
    /// Returns a log-safe string representation where card numbers are masked and CVVs are redacted.
    /// </summary>
    public override string ToString()
    {
        return $"CheckoutCommand {{ FlightId = {FlightId}, PassengerCount = {Passengers?.Count ?? 0}, ContactEmail = {Contact?.Email}, Card = {Card} }}";
    }
}
