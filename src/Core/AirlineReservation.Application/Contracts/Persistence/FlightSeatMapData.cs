using AirlineReservation.Domain.Enums;

namespace AirlineReservation.Application.Contracts.Persistence;

/// <summary>
/// Lean read-only projection for flight seat map rendering without tracking or entity graph bloat.
/// </summary>
public sealed record FlightSeatMapData(
    int FlightId,
    string FlightNumber,
    string AirlineName,
    int AircraftId,
    string AircraftModel,
    decimal BasePrice,
    DateTime DepartureTimeUtc,
    FlightStatus Status,
    IReadOnlyList<SeatMapSeatItem> Seats,
    IReadOnlySet<int> OccupiedSeatIds);

/// <summary>
/// Projected seat row attributes used to construct SeatDto instances.
/// </summary>
public sealed record SeatMapSeatItem(
    int Id,
    int AircraftId,
    string SeatNumber,
    int RowNumber,
    string ColumnLetter,
    SeatClass SeatClass,
    decimal BasePriceMultiplier,
    bool IsWindow,
    bool IsAisle,
    bool IsExitRow);
