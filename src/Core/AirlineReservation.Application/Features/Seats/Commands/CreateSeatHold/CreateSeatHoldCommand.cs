using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Seat;
using MediatR;

namespace AirlineReservation.Application.Features.Seats.Commands.CreateSeatHold;

/// <summary>
/// CQRS command to atomically hold 1 to 9 seats on a flight for the specified user.
/// </summary>
public sealed record CreateSeatHoldCommand(
    int FlightId,
    Guid UserId,
    IReadOnlyList<int> SeatIds) : IRequest<ApiResponse<SeatHoldResultDto>>;
