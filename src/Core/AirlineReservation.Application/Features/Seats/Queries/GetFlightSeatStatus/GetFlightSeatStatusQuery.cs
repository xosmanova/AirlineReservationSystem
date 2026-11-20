using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Seat;
using MediatR;

namespace AirlineReservation.Application.Features.Seats.Queries.GetFlightSeatStatus;

/// <summary>
/// CQRS Query for retrieving lightweight delta seat availability and hold statuses for interactive map polling.
/// Returns only non-available seats and generation timestamp.
/// </summary>
public sealed record GetFlightSeatStatusQuery(
    int FlightId,
    Guid? UserId = null) : IRequest<ApiResponse<SeatStatusSummaryDto>>;
