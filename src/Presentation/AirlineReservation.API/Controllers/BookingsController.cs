using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Booking;
using AirlineReservation.Application.Features.Bookings.Commands.CancelBooking;
using AirlineReservation.Application.Features.Bookings.Commands.Checkout;
using AirlineReservation.Application.Features.Bookings.Queries.CancelBookingPreview;
using AirlineReservation.Application.Features.Bookings.Queries.GetBookingByPnr;
using AirlineReservation.Application.Features.Bookings.Queries.GetBookingQuote;
using AirlineReservation.Application.Features.Bookings.Queries.GetMyBookings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirlineReservation.API.Controllers;

/// <summary>
/// Booking pricing quote, checkout, PNR lookup, and itinerary management endpoints.
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

    /// <summary>
    /// Completes flight checkout and purchases tickets for held seats in a single ACID transaction.
    /// </summary>
    /// <param name="command">Checkout payload containing flight, passengers, contact, and payment details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Standardized ApiResponse containing confirmed booking details and PNR code.</returns>
    [HttpPost("checkout")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CheckoutResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Checkout(
        [FromBody] CheckoutCommand command,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                          ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedException("Invalid or missing user identity in access token.");
        }

        command.UserId = userId;
        var result = await Mediator.Send(command, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Retrieves booking details by 6-character PNR code and passenger last name.
    /// Accessible anonymously so passengers can look up their itinerary without logging in.
    /// </summary>
    /// <param name="pnr">6-character alphanumeric PNR code.</param>
    /// <param name="lastName">Passenger's last name for identity confirmation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Standardized ApiResponse containing confirmed itinerary and ticket details.</returns>
    [HttpGet("pnr/{pnr}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<BookingDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByPnr(
        string pnr,
        [FromQuery] string lastName,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetBookingByPnrQuery(pnr, lastName), cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Retrieves a paginated list of bookings for the authenticated user, optionally filtered by status.
    /// </summary>
    /// <param name="status">Optional status filter: 'upcoming', 'past', or 'cancelled'.</param>
    /// <param name="pageNumber">Page index (1-based, default 1).</param>
    /// <param name="pageSize">Items per page (default 10, max 50).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Standardized ApiResponse containing paginated list of user booking summaries.</returns>
    [HttpGet("my")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<BookingDetailsDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyBookings(
        [FromQuery] string? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                          ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedException("Invalid or missing user identity in access token.");
        }

        var result = await Mediator.Send(
            new GetMyBookingsQuery(userId, status, pageNumber, pageSize),
            cancellationToken);

        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Previews the cancellation terms, refund amount, and penalty for a specific booking.
    /// Only the booking owner or an administrator can view this preview.
    /// </summary>
    /// <param name="id">Booking primary identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Standardized ApiResponse containing previewed refund calculation.</returns>
    [HttpGet("{id:int}/cancel-preview")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CancelBookingPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelPreview(
        int id,
        CancellationToken cancellationToken)
    {
        var (userId, isAdmin) = GetCurrentUserContext();
        var result = await Mediator.Send(new CancelBookingPreviewQuery(id, userId, isAdmin), cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Cancels a confirmed booking, marks tickets as cancelled to liberate seats, refunds payment
    /// according to policy rules, and records cancellation audit data in an ACID transaction.
    /// </summary>
    /// <param name="id">Booking primary identifier to cancel.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Standardized ApiResponse containing the updated booking status and refund details.</returns>
    [HttpPost("{id:int}/cancel")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<BookingDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(
        int id,
        CancellationToken cancellationToken)
    {
        var (userId, isAdmin) = GetCurrentUserContext();
        var result = await Mediator.Send(new CancelBookingCommand(id, userId, isAdmin), cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    private (Guid UserId, bool IsAdmin) GetCurrentUserContext()
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                          ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedException("Invalid or missing user identity in access token.");
        }

        var isAdmin = User.IsInRole("Admin") || User.HasClaim(c => c.Type == "role" && c.Value == "Admin");
        return (userId, isAdmin);
    }
}
