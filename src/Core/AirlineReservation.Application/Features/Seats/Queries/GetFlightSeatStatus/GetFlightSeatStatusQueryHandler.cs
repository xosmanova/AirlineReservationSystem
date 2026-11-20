using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Seat;
using AirlineReservation.Application.Features.Seats.Common;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using MediatR;

namespace AirlineReservation.Application.Features.Seats.Queries.GetFlightSeatStatus;

/// <summary>
/// CQRS Query Handler returning lightweight seat map status deltas (non-available seats only) with generation timestamp.
/// Uses SeatStatusResolver to guarantee 100% status parity with full seat map.
/// </summary>
public class GetFlightSeatStatusQueryHandler : IRequestHandler<GetFlightSeatStatusQuery, ApiResponse<SeatStatusSummaryDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public GetFlightSeatStatusQueryHandler(
        IUnitOfWork unitOfWork,
        TimeProvider? timeProvider = null)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ApiResponse<SeatStatusSummaryDto>> Handle(
        GetFlightSeatStatusQuery request,
        CancellationToken cancellationToken)
    {
        var seatMapData = await _unitOfWork.Flights.GetFlightSeatMapDataAsync(request.FlightId, cancellationToken);
        if (seatMapData == null)
        {
            throw new NotFoundException(nameof(Flight), request.FlightId);
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var activeHolds = await _unitOfWork.SeatHolds.GetActiveHoldsByFlightIdAsync(request.FlightId, utcNow, cancellationToken);
        var activeHoldsBySeatId = activeHolds.ToDictionary(h => h.SeatId);

        // Filter and project only non-Available seats to minimize network transfer
        var nonAvailableSeats = seatMapData.Seats
            .Select(s => SeatStatusResolver.Resolve(s.Id, seatMapData.OccupiedSeatIds, activeHoldsBySeatId, request.UserId))
            .Where(r => r.Status != SeatStatus.Available)
            .Select(r => new SeatStatusItemDto(r.SeatId, r.Status, r.HeldByMe))
            .ToList();

        return ApiResponse<SeatStatusSummaryDto>.SuccessResult(
            new SeatStatusSummaryDto(request.FlightId, utcNow, nonAvailableSeats),
            $"Seat status updates for flight {seatMapData.FlightNumber} retrieved successfully.");
    }
}
