using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Seat;
using MediatR;

namespace AirlineReservation.Application.Features.Seats.Queries.GetMySeatHolds;

/// <summary>
/// CQRS query to retrieve the active seat holds and remaining hold seconds on a flight for the authenticated user.
/// </summary>
public sealed record GetMySeatHoldsQuery(
    int FlightId,
    Guid UserId) : IRequest<ApiResponse<SeatHoldResultDto>>;
