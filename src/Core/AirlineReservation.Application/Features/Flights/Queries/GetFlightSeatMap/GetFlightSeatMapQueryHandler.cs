using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Seat;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using AirlineReservation.Domain.Specifications;
using MediatR;

namespace AirlineReservation.Application.Features.Flights.Queries.GetFlightSeatMap;

/// <summary>
/// CQRS Query Handler for generating interactive aircraft cabin seat maps with real-time seat statuses.
/// </summary>
public class GetFlightSeatMapQueryHandler : IRequestHandler<GetFlightSeatMapQuery, ApiResponse<SeatMapDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetFlightSeatMapQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ApiResponse<SeatMapDto>> Handle(GetFlightSeatMapQuery request, CancellationToken cancellationToken)
    {
        var flight = await _unitOfWork.Flights.GetFlightWithDetailsAsync(request.FlightId, cancellationToken);
        if (flight == null)
        {
            throw new NotFoundException(nameof(Flight), request.FlightId);
        }

        if (flight.Aircraft == null || flight.Aircraft.Seats == null || flight.Aircraft.Seats.Count == 0)
        {
            throw new NotFoundException($"Aircraft or seat layout for flight {request.FlightId} was not found.");
        }

        // Active occupied seats: ticket is active according to unified domain specification
        var occupiedSeatIds = flight.Tickets
            .Where(TicketSpecifications.IsActiveCompiled)
            .Select(t => t.SeatId)
            .ToHashSet();

        // Project all seats with accurate seat-class multipliers and availability status.
        // Held/HeldByMe become active in Day 14 (SeatHold) and Blocked in Day 31 (FlightSeatBlock).
        var seats = flight.Aircraft.Seats
            .OrderBy(s => s.RowNumber)
            .ThenBy(s => s.ColumnLetter)
            .Select(s => new SeatDto
            {
                Id = s.Id,
                AircraftId = s.AircraftId,
                SeatNumber = s.SeatNumber,
                RowNumber = s.RowNumber,
                ColumnLetter = s.ColumnLetter,
                SeatClass = s.SeatClass,
                BasePriceMultiplier = s.BasePriceMultiplier,
                Price = Math.Round(flight.BasePrice * (s.BasePriceMultiplier > 0 ? s.BasePriceMultiplier : 1.0m), 2),
                Status = occupiedSeatIds.Contains(s.Id) ? SeatStatus.Occupied : SeatStatus.Available,
                HeldByMe = false,
                IsWindow = s.IsWindow,
                IsAisle = s.IsAisle,
                IsExitRow = s.IsExitRow
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
        int occupiedCount = seats.Count(s => s.Status != SeatStatus.Available);
        int availableCount = Math.Max(0, totalSeats - occupiedCount);

        var dto = new SeatMapDto
        {
            FlightId = flight.Id,
            FlightNumber = flight.FlightNumber,
            AirlineName = flight.Airline?.Name ?? string.Empty,
            AircraftModel = flight.Aircraft.Model,
            TotalSeats = totalSeats,
            AvailableSeatsCount = availableCount,
            OccupiedSeatsCount = occupiedCount,
            BasePrice = flight.BasePrice,
            Rows = rows
        };

        return ApiResponse<SeatMapDto>.SuccessResult(
            dto,
            $"Interactive seat map for flight {flight.FlightNumber} generated successfully.");
    }
}
