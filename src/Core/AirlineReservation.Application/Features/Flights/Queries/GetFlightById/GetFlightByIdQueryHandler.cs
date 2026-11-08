using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Mappings;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Flight;
using AirlineReservation.Domain.Enums;
using MediatR;

namespace AirlineReservation.Application.Features.Flights.Queries.GetFlightById;

/// <summary>
/// CQRS Query Handler for fetching a single flight with real-time seat availability.
/// </summary>
public class GetFlightByIdQueryHandler : IRequestHandler<GetFlightByIdQuery, ApiResponse<FlightDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AirlineMappingProfile _mapper;

    public GetFlightByIdQueryHandler(IUnitOfWork unitOfWork, AirlineMappingProfile mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ApiResponse<FlightDto>> Handle(GetFlightByIdQuery request, CancellationToken cancellationToken)
    {
        var flight = await _unitOfWork.Flights.GetFlightWithDetailsAsync(request.Id, cancellationToken);
        if (flight == null)
        {
            throw new NotFoundException($"Flight with ID {request.Id} was not found.");
        }

        var dto = _mapper.ToDto(flight);

        int occupiedSeatsCount = flight.Tickets.Count(t =>
            !t.IsDeleted &&
            !t.IsCancelled &&
            t.Booking != null &&
            !t.Booking.IsDeleted &&
            t.Booking.Status != BookingStatus.Cancelled &&
            t.Booking.Status != BookingStatus.Expired);

        int totalSeats = flight.Aircraft?.TotalSeats ?? 0;
        dto.AvailableSeatsCount = Math.Max(0, totalSeats - occupiedSeatsCount);
        dto.TotalSeats = totalSeats;
        dto.AircraftModel = flight.Aircraft?.Model ?? string.Empty;

        return ApiResponse<FlightDto>.SuccessResult(dto, "Flight retrieved successfully.");
    }
}
