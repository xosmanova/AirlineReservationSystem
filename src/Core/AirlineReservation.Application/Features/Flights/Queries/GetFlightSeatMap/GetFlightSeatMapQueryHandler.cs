using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Seat;
using AirlineReservation.Application.Features.Seats.Common;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using MediatR;

namespace AirlineReservation.Application.Features.Flights.Queries.GetFlightSeatMap;

/// <summary>
/// CQRS Query Handler for generating interactive aircraft cabin seat maps with real-time seat statuses.
/// </summary>
public class GetFlightSeatMapQueryHandler : IRequestHandler<GetFlightSeatMapQuery, ApiResponse<SeatMapDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPricingService _pricingService;
    private readonly TimeProvider _timeProvider;

    public GetFlightSeatMapQueryHandler(
        IUnitOfWork unitOfWork,
        IPricingService pricingService,
        TimeProvider? timeProvider = null)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _pricingService = pricingService ?? throw new ArgumentNullException(nameof(pricingService));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ApiResponse<SeatMapDto>> Handle(GetFlightSeatMapQuery request, CancellationToken cancellationToken)
    {
        var seatMapData = await _unitOfWork.Flights.GetFlightSeatMapDataAsync(request.FlightId, cancellationToken);
        if (seatMapData == null)
        {
            throw new NotFoundException(nameof(Flight), request.FlightId);
        }

        if (seatMapData.Seats.Count == 0)
        {
            throw new NotFoundException($"Aircraft or seat layout for flight {request.FlightId} was not found.");
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var activeHolds = await _unitOfWork.SeatHolds.GetActiveHoldsByFlightIdAsync(request.FlightId, utcNow, cancellationToken);
        var activeHoldsBySeatId = activeHolds.ToDictionary(h => h.SeatId);

        // Map pre-projected seats with unified SeatStatusResolver.
        // Status precedence: Blocked > Occupied > Held > Available.
        var seats = seatMapData.Seats
            .OrderBy(s => s.RowNumber)
            .ThenBy(s => s.ColumnLetter)
            .Select(s =>
            {
                var resolved = SeatStatusResolver.Resolve(s.Id, seatMapData.OccupiedSeatIds, activeHoldsBySeatId, request.UserId);

                return new SeatDto
                {
                    Id = s.Id,
                    AircraftId = s.AircraftId,
                    SeatNumber = s.SeatNumber,
                    RowNumber = s.RowNumber,
                    ColumnLetter = s.ColumnLetter,
                    SeatClass = s.SeatClass,
                    BasePriceMultiplier = s.BasePriceMultiplier,
                    Price = _pricingService.CalculateSeatPrice(new SeatPricingInput(
                        seatMapData.BasePrice,
                        s.BasePriceMultiplier,
                        s.IsWindow,
                        s.IsAisle,
                        seatMapData.DepartureTimeUtc)).FinalPrice,
                    Status = resolved.Status,
                    HeldByMe = resolved.HeldByMe,
                    IsWindow = s.IsWindow,
                    IsAisle = s.IsAisle,
                    IsExitRow = s.IsExitRow
                };
            })
            .ToList();

        // Group into cabin rows
        var rows = seats
            .GroupBy(s => s.RowNumber)
            .OrderBy(g => g.Key)
            .Select(g => new SeatRowDto
            {
                RowNumber = g.Key,
                SeatClass = g.First().SeatClass,
                IsExitRow = g.Any(s => s.IsExitRow),
                Seats = g.OrderBy(s => s.ColumnLetter).ToList()
            })
            .ToList();

        int totalSeats = seats.Count;
        int heldCount = seats.Count(s => s.Status == SeatStatus.Held);
        int occupiedCount = seats.Count(s => s.Status != SeatStatus.Available);
        int availableCount = Math.Max(0, totalSeats - occupiedCount);

        var dto = new SeatMapDto
        {
            FlightId = seatMapData.FlightId,
            FlightNumber = seatMapData.FlightNumber,
            AirlineName = seatMapData.AirlineName,
            AircraftModel = seatMapData.AircraftModel,
            TotalSeats = totalSeats,
            AvailableSeatsCount = availableCount,
            OccupiedSeatsCount = occupiedCount,
            HeldSeatsCount = heldCount,
            BasePrice = seatMapData.BasePrice,
            Rows = rows
        };

        return ApiResponse<SeatMapDto>.SuccessResult(
            dto,
            $"Interactive seat map for flight {seatMapData.FlightNumber} generated successfully.");
    }
}
