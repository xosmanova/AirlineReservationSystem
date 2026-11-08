using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Flight;
using MediatR;

namespace AirlineReservation.Application.Features.Flights.Queries.GetFlightById;

/// <summary>
/// CQRS Query for retrieving complete flight details by unique Flight ID.
/// </summary>
public record GetFlightByIdQuery(int Id) : IRequest<ApiResponse<FlightDto>>;
