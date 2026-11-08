using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Flight;
using MediatR;

namespace AirlineReservation.Application.Features.Flights.Queries.SearchFlights;

/// <summary>
/// CQRS Query for searching available scheduled flights with filtering, sorting, and pagination criteria.
/// </summary>
public record SearchFlightsQuery(
    int DepartureAirportId,
    int ArrivalAirportId,
    DateTime DepartureDate,
    int? PassengersCount = 1,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    int? AirlineId = null,
    int? MaxStops = null,
    string? SortBy = "departure",
    string? SortDirection = "asc",
    int Page = 1,
    int PageSize = 10) : IRequest<ApiResponse<IReadOnlyList<FlightDto>>>;
