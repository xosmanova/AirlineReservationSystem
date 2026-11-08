using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Flight;
using AirlineReservation.Application.DTOs.Seat;
using AirlineReservation.Application.Features.Flights.Queries.GetFlightById;
using AirlineReservation.Application.Features.Flights.Queries.GetFlightSeatMap;
using AirlineReservation.Application.Features.Flights.Queries.SearchFlights;
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
    /// Retrieves interactive aircraft cabin seat map with real-time seat occupancy and dynamic pricing.
    /// </summary>
    /// <param name="id">Unique identifier of the flight.</param>
    /// <returns>Standardized ApiResponse containing structured seat map rows and embedded SVG markup.</returns>
    [HttpGet("{id:int}/seatmap")]
    [ProducesResponseType(typeof(ApiResponse<SeatMapDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSeatMap(int id)
    {
        var result = await Mediator.Send(new GetFlightSeatMapQuery(id));
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Renders standalone, interactive SVG aircraft cabin map for direct browser display or iframe embedding.
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

        return Content(result.Data.SvgMap, "image/svg+xml", System.Text.Encoding.UTF8);
    }
}
