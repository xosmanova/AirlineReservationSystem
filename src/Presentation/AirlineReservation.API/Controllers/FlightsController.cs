using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Generators;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Flight;
using AirlineReservation.Application.DTOs.Seat;
using AirlineReservation.Application.Features.Flights.Queries.GetFlightById;
using AirlineReservation.Application.Features.Flights.Queries.GetFlightSeatMap;
using AirlineReservation.Application.Features.Flights.Queries.SearchFlights;
using AirlineReservation.Application.Features.Seats.Commands.CreateSeatHold;
using AirlineReservation.Application.Features.Seats.Commands.ReleaseSeatHolds;
using AirlineReservation.Application.Features.Seats.Queries.GetMySeatHolds;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirlineReservation.API.Controllers;

/// <summary>
/// RESTful Controller for flight search, details, and interactive seat map operations.
/// </summary>
public class FlightsController : BaseApiController
{
    /// <summary>
    /// Searches available scheduled flights by departure/arrival airports, date, and optional filters.
    /// </summary>
    /// <param name="query">Search criteria including routes, dates, passengers, price, and stops.</param>
    /// <returns>Standardized ApiResponse containing matching flights with available seat counts.</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<FlightDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromQuery] SearchFlightsQuery query)
    {
        var result = await Mediator.Send(query);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Retrieves complete details of a specific flight by its unique ID.
    /// </summary>
    /// <param name="id">Unique identifier of the flight.</param>
    /// <returns>Standardized ApiResponse containing flight details and current seat availability.</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<FlightDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetFlightByIdQuery(id));
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Retrieves structured aircraft cabin seat map with real-time seat statuses and dynamic pricing.
    /// </summary>
    /// <param name="id">Unique identifier of the flight.</param>
    /// <returns>Standardized ApiResponse containing structured seat map rows.</returns>
    [HttpGet("{id:int}/seatmap")]
    [ProducesResponseType(typeof(ApiResponse<SeatMapDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSeatMap(int id)
    {
        var result = await Mediator.Send(new GetFlightSeatMapQuery(id));
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Renders standalone SVG aircraft cabin map for direct browser display or iframe embedding.
    /// </summary>
    /// <param name="id">Unique identifier of the flight.</param>
    /// <returns>Raw SVG XML content with image/svg+xml MIME type.</returns>
    [HttpGet("{id:int}/seatmap/svg")]
    [Produces("image/svg+xml")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSeatMapSvg(int id)
    {
        var result = await Mediator.Send(new GetFlightSeatMapQuery(id));
        if (!result.Success || result.Data == null)
        {
            return NotFound(result);
        }

        string svgMap = SvgSeatMapGenerator.Generate(
            result.Data.FlightNumber,
            result.Data.AirlineName,
            result.Data.AircraftModel,
            result.Data.Rows);

        return Content(svgMap, "image/svg+xml", System.Text.Encoding.UTF8);
    }

    /// <summary>
    /// Atomically reserves 1 to 9 seats on a flight for the authenticated user for 10 minutes.
    /// </summary>
    /// <param name="id">Unique identifier of the flight.</param>
    /// <param name="dto">Request payload containing up to 9 distinct seat IDs.</param>
    /// <returns>Standardized ApiResponse containing held seat items and server-computed remaining seconds.</returns>
    [HttpPost("{id:int}/holds")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SeatHoldResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSeatHold(int id, [FromBody] SeatHoldRequestDto dto)
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                          ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedException("Invalid or missing user identity in access token.");
        }

        var command = new CreateSeatHoldCommand(id, userId, dto?.SeatIds ?? Array.Empty<int>());
        var result = await Mediator.Send(command);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Retrieves active seat holds on a specific flight for the authenticated user, including server-computed remaining seconds.
    /// </summary>
    /// <param name="id">Unique identifier of the flight.</param>
    /// <returns>Standardized ApiResponse containing held seats and remaining hold seconds.</returns>
    [HttpGet("{id:int}/holds/mine")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SeatHoldResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyHolds(int id)
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                          ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedException("Invalid or missing user identity in access token.");
        }

        var query = new GetMySeatHoldsQuery(id, userId);
        var result = await Mediator.Send(query);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Idempotently releases all active seat holds on a specific flight for the authenticated user.
    /// Leaves holds belonging to other passengers completely untouched.
    /// </summary>
    /// <param name="id">Unique identifier of the flight.</param>
    /// <returns>Standardized ApiResponse confirming release of holds.</returns>
    [HttpDelete("{id:int}/holds")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReleaseMyHolds(int id)
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                          ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedException("Invalid or missing user identity in access token.");
        }

        var command = new ReleaseSeatHoldsCommand(id, userId);
        var result = await Mediator.Send(command);
        return StatusCode(result.StatusCode, result);
    }
}
