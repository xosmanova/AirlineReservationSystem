using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Domain.Entities;
using MediatR;

namespace AirlineReservation.Application.Features.Seats.Commands.ReleaseSeatHolds;

/// <summary>
/// Handles idempotent release of the authenticated user's seat holds on a specific flight.
/// Only deletes the caller's hold rows and leaves other passengers' holds untouched.
/// </summary>
public class ReleaseSeatHoldsCommandHandler : IRequestHandler<ReleaseSeatHoldsCommand, ApiResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public ReleaseSeatHoldsCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ApiResponse> Handle(
        ReleaseSeatHoldsCommand request,
        CancellationToken cancellationToken)
    {
        var flight = await _unitOfWork.Flights.GetByIdAsync(request.FlightId, cancellationToken);
        if (flight == null)
        {
            throw new NotFoundException(nameof(Flight), request.FlightId);
        }

        int releasedCount = await _unitOfWork.SeatHolds.ReleaseUserHoldsForFlightAsync(
            request.FlightId,
            request.UserId,
            cancellationToken);

        return ApiResponse.SuccessResult(
            releasedCount > 0
                ? $"{releasedCount} seat hold(s) released successfully."
                : "No active seat holds were found to release.");
    }
}
