using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Booking;
using MediatR;

namespace AirlineReservation.Application.Features.Bookings.Queries.GetBookingQuote;

/// <summary>
/// CQRS query for calculating an informational multi-seat price quote for a sellable flight.
/// </summary>
/// <remarks>
/// Quote responses are informational only; checkout recalculates prices server-side.
/// </remarks>
public sealed record GetBookingQuoteQuery(
    int FlightId,
    IReadOnlyList<int> SeatIds) : IRequest<ApiResponse<BookingQuoteDto>>;
