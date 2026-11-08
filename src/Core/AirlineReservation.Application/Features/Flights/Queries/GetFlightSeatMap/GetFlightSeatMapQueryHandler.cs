using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Generators;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Seat;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using MediatR;

namespace AirlineReservation.Application.Features.Flights.Queries.GetFlightSeatMap;

/// <summary>
/// CQRS Query Handler for generating interactive aircraft cabin seat maps with real-time occupancy.
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

        // Active occupied seats: ticket is active and booking is not cancelled/expired
        var occupiedSeatIds = flight.Tickets
            .Where(t => !t.IsDeleted &&
                        !t.IsCancelled &&
                        t.Booking != null &&
                        !t.Booking.IsDeleted &&
                        t.Booking.Status != BookingStatus.Cancelled &&
                        t.Booking.Status != BookingStatus.Expired)
            .Select(t => t.SeatId)
            .ToHashSet();

        // Project all seats with accurate seat-class multipliers and occupancy
        var flightSeats = flight.Aircraft.Seats
            .OrderBy(s => s.RowNumber)
            .ThenBy(s => s.ColumnLetter)
            .Select(s => new FlightSeatDto
            {
                Id = s.Id,
                SeatNumber = s.SeatNumber,
                RowNumber = s.RowNumber,
                ColumnLetter = s.ColumnLetter,
                SeatClass = s.SeatClass,
                BasePriceMultiplier = s.BasePriceMultiplier,
                Price = Math.Round(flight.BasePrice * (s.BasePriceMultiplier > 0 ? s.BasePriceMultiplier : 1.0m), 2),
                IsOccupied = occupiedSeatIds.Contains(s.Id),
                IsWindow = s.IsWindow,
                IsAisle = s.IsAisle,
                IsExitRow = s.IsExitRow
            })
            .ToList();

        // Group into cabin rows
        var rows = flightSeats
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

        // Generate dynamic interactive SVG seat map
        string svgMap = SvgSeatMapGenerator.Generate(
            flight.FlightNumber,
            flight.Airline?.Name ?? "Airline",
            flight.Aircraft.Model,
            rows);

        int totalSeats = flightSeats.Count;
        int occupiedCount = flightSeats.Count(s => s.IsOccupied);
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
            Rows = rows,
            SvgMap = svgMap
        };

        return ApiResponse<SeatMapDto>.SuccessResult(
            dto,
            $"Interactive seat map for flight {flight.FlightNumber} generated successfully.");
    }
}
