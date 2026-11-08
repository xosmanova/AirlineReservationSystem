using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Seat;
using MediatR;

namespace AirlineReservation.Application.Features.Flights.Queries.GetFlightSeatMap;

/// <summary>
/// CQRS Query for retrieving complete interactive seat map and cabin layout for a specific flight.
/// </summary>
public record GetFlightSeatMapQuery(int FlightId) : IRequest<ApiResponse<SeatMapDto>>;
