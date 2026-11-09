using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Airline;
using AirlineReservation.Application.Features.Airlines.Queries.GetAllAirlines;
using Microsoft.AspNetCore.Mvc;

namespace AirlineReservation.API.Controllers;

/// <summary>
/// RESTful Controller for airline carrier metadata and catalog operations.
/// </summary>
public class AirlinesController : BaseApiController
{
    /// <summary>
    /// Retrieves all active airline carriers for flight filtering and catalog display.
    /// </summary>
    /// <returns>Standardized ApiResponse containing all registered airlines.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AirlineDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var result = await Mediator.Send(new GetAllAirlinesQuery());
        return StatusCode(result.StatusCode, result);
    }
}
