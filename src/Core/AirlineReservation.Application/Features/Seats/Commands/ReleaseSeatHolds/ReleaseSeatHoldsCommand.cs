using AirlineReservation.Application.Common.Models;
using MediatR;

namespace AirlineReservation.Application.Features.Seats.Commands.ReleaseSeatHolds;

/// <summary>
/// CQRS command to idempotently release all active seat holds on a flight for the authenticated user.
/// </summary>
public sealed record ReleaseSeatHoldsCommand(
    int FlightId,
    Guid UserId) : IRequest<ApiResponse>;
