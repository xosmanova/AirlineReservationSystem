using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Flight;
using MediatR;

namespace AirlineReservation.Application.Features.Flights.Queries.SearchFlights;

/// <summary>
/// CQRS Query Handler for flight search.
/// Executes optimized SQL search with timezone-aware boundaries, sellable statuses,
/// SQL-level seat availability filtering, and paginated projection.
/// </summary>
public class SearchFlightsQueryHandler : IRequestHandler<SearchFlightsQuery, ApiResponse<PagedResult<FlightDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBusinessTime _businessTime;
    private readonly TimeProvider _timeProvider;

    public SearchFlightsQueryHandler(
        IUnitOfWork unitOfWork,
        IBusinessTime businessTime,
        TimeProvider? timeProvider = null)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _businessTime = businessTime ?? throw new ArgumentNullException(nameof(businessTime));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ApiResponse<PagedResult<FlightDto>>> Handle(SearchFlightsQuery request, CancellationToken cancellationToken)
    {
        var startUtc = _businessTime.StartOfLocalDayUtc(request.DepartureDate);
        var endUtc = _businessTime.EndOfLocalDayUtc(request.DepartureDate);
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        int requiredPassengers = request.PassengersCount.GetValueOrDefault(1);
        if (requiredPassengers <= 0) requiredPassengers = 1;

        var (items, totalCount) = await _unitOfWork.Flights.SearchFlightsAsync(
            request.DepartureAirportId,
            request.ArrivalAirportId,
            startUtc,
            endUtc,
            nowUtc,
            requiredPassengers,
            request.MinPrice,
            request.MaxPrice,
            request.AirlineId,
            request.MaxStops,
            request.SortBy,
            request.SortDirection,
            request.Page,
            request.PageSize,
            cancellationToken);

        var pagedResult = new PagedResult<FlightDto>(items, totalCount, request.Page, request.PageSize);

        string message = totalCount > 0
            ? $"Found {totalCount} flight(s) matching your criteria."
            : "No flights found matching the specified search criteria.";

        return ApiResponse<PagedResult<FlightDto>>.SuccessResult(pagedResult, message);
    }
}
