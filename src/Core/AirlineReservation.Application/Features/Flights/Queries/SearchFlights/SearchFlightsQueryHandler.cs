using AirlineReservation.Application.Common.Mappings;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Flight;
using AirlineReservation.Domain.Enums;
using MediatR;

namespace AirlineReservation.Application.Features.Flights.Queries.SearchFlights;

/// <summary>
/// CQRS Query Handler for flight search.
/// Applies search filters, calculates real-time seat availability, and projects to DTOs.
/// </summary>
public class SearchFlightsQueryHandler : IRequestHandler<SearchFlightsQuery, ApiResponse<IReadOnlyList<FlightDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AirlineMappingProfile _mapper;

    public SearchFlightsQueryHandler(IUnitOfWork unitOfWork, AirlineMappingProfile mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ApiResponse<IReadOnlyList<FlightDto>>> Handle(SearchFlightsQuery request, CancellationToken cancellationToken)
    {
        var flights = await _unitOfWork.Flights.SearchFlightsAsync(
            request.DepartureAirportId,
            request.ArrivalAirportId,
            request.DepartureDate,
            request.MaxPrice,
            request.AirlineId,
            request.MaxStops,
            request.SortBy,
            request.SortDirection,
            cancellationToken);

        int requiredPassengers = request.PassengersCount.GetValueOrDefault(1);
        if (requiredPassengers <= 0) requiredPassengers = 1;

        var resultList = new List<FlightDto>();

        foreach (var flight in flights)
        {
            var dto = _mapper.ToDto(flight);

            // Calculate occupied seats: ticket is not deleted/cancelled AND booking is active
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

            // Filter flights that have sufficient seats for the requested passengers
            if (dto.AvailableSeatsCount >= requiredPassengers)
            {
                resultList.Add(dto);
            }
        }

        return ApiResponse<IReadOnlyList<FlightDto>>.SuccessResult(
            resultList,
            resultList.Count > 0
                ? $"Found {resultList.Count} available flight(s)."
                : "No available flights found matching your search criteria.");
    }
}
