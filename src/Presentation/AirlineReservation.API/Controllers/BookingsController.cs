using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Booking;
using AirlineReservation.Application.Features.Bookings.Queries.GetBookingQuote;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirlineReservation.API.Controllers;

/// <summary>
/// Booking pricing quote and reservation endpoints.
/// </summary>
public class BookingsController : BaseApiController
{
    /// <summary>
    /// Calculates an itemized server-side seat price quote for the requested flight and seat IDs.
    /// </summary>
    /// <remarks>
    /// Accessible anonymously so the seat selection UI can display live pricing before login.
    /// The quote is informational only and does not hold seats; checkout recalculates final prices server-side.
    /// </remarks>
    [HttpPost("quote")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<BookingQuoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Quote(
        [FromBody] BookingQuoteRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new GetBookingQuoteQuery(request.FlightId, request.SeatIds ?? []),
            cancellationToken);

        return StatusCode(result.StatusCode, result);
    }
}
