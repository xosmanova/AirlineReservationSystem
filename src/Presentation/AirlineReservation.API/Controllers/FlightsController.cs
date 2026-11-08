using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Flight;
using AirlineReservation.Application.Features.Flights.Queries.GetFlightById;
using AirlineReservation.Application.Features.Flights.Queries.SearchFlights;
using Microsoft.AspNetCore.Mvc;

namespace AirlineReservation.API.Controllers;

/// <summary>
/// RESTful Controller for flight search and retrieval operations.
/// </summary>
public class FlightsController : BaseApiController
{
    /// <summary>
    /// Searches available scheduled flights by departure/arrival airports, date, and optional filters.
    /// </summary>
    /// <param name="query">Search criteria including routes, dates, passengers, price, and stops.</param>
    /// <returns>Standardized ApiResponse containing matching flights with available seat counts.</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<FlightDto>>), StatusCodes.Status200OK)]
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
}
