using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Airline;
using MediatR;

namespace AirlineReservation.Application.Features.Airlines.Queries.GetAllAirlines;

/// <summary>
/// CQRS Query for retrieving all active airlines for dropdown filters and catalog display.
/// </summary>
public record GetAllAirlinesQuery : IRequest<ApiResponse<IReadOnlyList<AirlineDto>>>;
