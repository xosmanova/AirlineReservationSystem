using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Seat;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using MediatR;

namespace AirlineReservation.Application.Features.Seats.Commands.CreateSeatHold;

/// <summary>
/// Handles atomic multi-seat hold creation within an ACID database transaction.
/// Translates unique constraint collisions on (FlightId, SeatId) into meaningful 409 ConflictException.
/// </summary>
public class CreateSeatHoldCommandHandler : IRequestHandler<CreateSeatHoldCommand, ApiResponse<SeatHoldResultDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly HoldSettings _holdSettings;

    public CreateSeatHoldCommandHandler(
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        HoldSettings? holdSettings = null)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _holdSettings = holdSettings ?? new HoldSettings();
    }

    public async Task<ApiResponse<SeatHoldResultDto>> Handle(
        CreateSeatHoldCommand request,
        CancellationToken cancellationToken)
    {
        var seatMapData = await _unitOfWork.Flights.GetFlightSeatMapDataAsync(request.FlightId, cancellationToken);
        if (seatMapData == null)
        {
            throw new NotFoundException(nameof(Flight), request.FlightId);
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var departureUtc = DateTime.SpecifyKind(seatMapData.DepartureTimeUtc, DateTimeKind.Utc);
        bool isSellable = seatMapData.Status is FlightStatus.Scheduled or FlightStatus.Delayed
                          && departureUtc > utcNow;

        if (!isSellable)
        {
            throw new ConflictException(
                $"Flight {seatMapData.FlightNumber} is not sellable (status: {seatMapData.Status}).");
        }

        var aircraftSeatsById = seatMapData.Seats.ToDictionary(s => s.Id);
        var missingFromAircraft = request.SeatIds
            .Where(id => !aircraftSeatsById.ContainsKey(id))
            .ToList();

        if (missingFromAircraft.Count > 0)
        {
            var seatsOnOtherAircraft = await _unitOfWork.Seats.FindAsync(
                s => missingFromAircraft.Contains(s.Id),
                cancellationToken);

            var foundElsewhereIds = seatsOnOtherAircraft.Select(s => s.Id).ToHashSet();
            var unknownSeatIds = missingFromAircraft
                .Where(id => !foundElsewhereIds.Contains(id))
                .ToList();

            if (unknownSeatIds.Count > 0)
            {
                throw new NotFoundException($"Seat(s) not found: {string.Join(", ", unknownSeatIds)}.");
            }

            var mismatchedLabels = seatsOnOtherAircraft.Select(s => $"{s.SeatNumber} (Id: {s.Id})");
            throw new BadRequestException(
                $"Seat(s) {string.Join(", ", mismatchedLabels)} do not belong to flight {seatMapData.FlightNumber}'s aircraft.");
        }

        var occupiedSeatNumbers = request.SeatIds
            .Where(id => seatMapData.OccupiedSeatIds.Contains(id))
            .Select(id => aircraftSeatsById[id].SeatNumber)
            .ToList();

        if (occupiedSeatNumbers.Count > 0)
        {
            throw new ConflictException(
                $"The following seat(s) are already occupied: {string.Join(", ", occupiedSeatNumbers)}.");
        }

        var durationMinutes = _holdSettings.Minutes > 0 ? _holdSettings.Minutes : 10;
        var expiresAtUtc = utcNow.AddMinutes(durationMinutes);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // 1. Hold-spam prevention: User can only hold seats on one flight at a time.
            // Release holds on any other flight for this user.
            await _unitOfWork.SeatHolds.ReleaseOtherFlightHoldsForUserAsync(request.FlightId, request.UserId, cancellationToken);

            // 2. Hold rule: Expired holds on the selected seats must be deleted before checking availability.
            await _unitOfWork.SeatHolds.DeleteExpiredHoldsForSeatsAsync(request.FlightId, request.SeatIds, utcNow, cancellationToken);

            // 3. Check if any selected seat is currently held by ANOTHER passenger.
            var activeHolds = await _unitOfWork.SeatHolds.GetActiveHoldsByFlightIdAsync(request.FlightId, utcNow, cancellationToken);
            var heldByOthers = activeHolds
                .Where(h => request.SeatIds.Contains(h.SeatId) && h.UserId != request.UserId)
                .Select(h => aircraftSeatsById[h.SeatId].SeatNumber)
                .Distinct()
                .ToList();

            if (heldByOthers.Count > 0)
            {
                throw new ConflictException(
                    $"The following seat(s) are already held by another passenger: {string.Join(", ", heldByOthers)}.");
            }

            // 4. Release this user's existing holds on this flight (replacing selection).
            await _unitOfWork.SeatHolds.ReleaseUserHoldsForFlightAsync(request.FlightId, request.UserId, cancellationToken);

            // 5. Create new SeatHold records for requested seats.
            var newHolds = request.SeatIds
                .Select(seatId => new SeatHold
                {
                    FlightId = request.FlightId,
                    SeatId = seatId,
                    UserId = request.UserId,
                    CreatedAtUtc = utcNow,
                    ExpiresAtUtc = expiresAtUtc
                })
                .ToList();

            await _unitOfWork.SeatHolds.AddRangeAsync(newHolds, cancellationToken);

            // 6. Commit transaction (UnitOfWork catches SQL 2601/2627 and translates to UniqueConstraintViolationException)
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            var remainingSeconds = (int)Math.Max(0, Math.Ceiling((expiresAtUtc - utcNow).TotalSeconds));
            var heldItems = request.SeatIds
                .OrderBy(id => aircraftSeatsById[id].RowNumber)
                .ThenBy(id => aircraftSeatsById[id].ColumnLetter)
                .Select(id => new HeldSeatItemDto(
                    id,
                    aircraftSeatsById[id].SeatNumber,
                    expiresAtUtc,
                    remainingSeconds))
                .ToList();

            return ApiResponse<SeatHoldResultDto>.SuccessResult(
                new SeatHoldResultDto(
                    request.FlightId,
                    heldItems,
                    expiresAtUtc,
                    remainingSeconds),
                "Seats successfully reserved.");
        }
        catch (UniqueConstraintViolationException ex)
        {
            // UnitOfWork.CommitTransactionAsync already rolled back the transaction
            throw new ConflictException("One or more selected seats were just held by another passenger.", ex);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
