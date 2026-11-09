using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Airport;
using AirlineReservation.Application.Features.Airports.Queries.SearchAirports;
using Microsoft.AspNetCore.Mvc;

namespace AirlineReservation.API.Controllers;

/// <summary>
/// RESTful Controller for airport lookups and autocomplete search.
/// </summary>
public class AirportsController : BaseApiController
{
    /// <summary>
    /// Searches airports for frontend autocomplete dropdowns by IATA code, city, name, or localized aliases (e.g. "Bakı", "baku", "Баку", "GYD").
    /// </summary>
    /// <param name="search">Optional search term or partial string (max 50 characters).</param>
    /// <param name="limit">Maximum number of results to return (default 10, max 50).</param>
    /// <returns>Standardized ApiResponse containing matching airports.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AirportDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromQuery] string? search, [FromQuery] int limit = 10)
    {
        var result = await Mediator.Send(new SearchAirportsQuery(search, limit));
        return StatusCode(result.StatusCode, result);
    }
}
