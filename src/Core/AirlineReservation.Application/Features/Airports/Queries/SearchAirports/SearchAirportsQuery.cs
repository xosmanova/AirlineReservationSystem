using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Airport;
using MediatR;

namespace AirlineReservation.Application.Features.Airports.Queries.SearchAirports;

/// <summary>
/// CQRS Query for searching airports for lookup and autocomplete dropdowns.
/// Supports search by IATA code (e.g. GYD), city name (e.g. Baku), airport name, or localized alias (e.g. Bakı, Баку).
/// </summary>
/// <param name="Search">Search term or partial keyword.</param>
/// <param name="Limit">Maximum number of results to return (default 10, max 50).</param>
public record SearchAirportsQuery(string? Search = null, int Limit = 10) : IRequest<ApiResponse<IReadOnlyList<AirportDto>>>;
