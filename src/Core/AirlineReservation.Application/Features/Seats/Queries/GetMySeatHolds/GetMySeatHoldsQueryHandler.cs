using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Seat;
using AirlineReservation.Domain.Entities;
using MediatR;

namespace AirlineReservation.Application.Features.Seats.Queries.GetMySeatHolds;

/// <summary>
/// Handles retrieving personal active seat holds on a specific flight with server-computed remaining seconds.
/// </summary>
public class GetMySeatHoldsQueryHandler : IRequestHandler<GetMySeatHoldsQuery, ApiResponse<SeatHoldResultDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public GetMySeatHoldsQueryHandler(
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<ApiResponse<SeatHoldResultDto>> Handle(
        GetMySeatHoldsQuery request,
        CancellationToken cancellationToken)
    {
        var flight = await _unitOfWork.Flights.GetByIdAsync(request.FlightId, cancellationToken);
        if (flight == null)
        {
            throw new NotFoundException(nameof(Flight), request.FlightId);
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var activeHolds = await _unitOfWork.SeatHolds.GetUserActiveHoldsForFlightAsync(
            request.FlightId,
            request.UserId,
            utcNow,
            cancellationToken);

        if (activeHolds.Count == 0)
        {
            return ApiResponse<SeatHoldResultDto>.SuccessResult(
                new SeatHoldResultDto(
                    request.FlightId,
                    Array.Empty<HeldSeatItemDto>(),
                    utcNow,
                    0),
                "No active seat holds found for this flight.");
        }

        var heldItems = activeHolds
            .Select(h =>
            {
                var remaining = (int)Math.Max(0, Math.Ceiling((h.ExpiresAtUtc - utcNow).TotalSeconds));
                return new HeldSeatItemDto(
                    h.SeatId,
                    h.Seat?.SeatNumber ?? string.Empty,
                    h.ExpiresAtUtc,
                    remaining);
            })
            .ToList();

        var overallRemainingSeconds = heldItems.Max(i => i.RemainingSeconds);
        var latestExpiry = activeHolds.Max(h => h.ExpiresAtUtc);

        return ApiResponse<SeatHoldResultDto>.SuccessResult(
            new SeatHoldResultDto(
                request.FlightId,
                heldItems,
                latestExpiry,
                overallRemainingSeconds),
            "Active seat holds retrieved successfully.");
    }
}
