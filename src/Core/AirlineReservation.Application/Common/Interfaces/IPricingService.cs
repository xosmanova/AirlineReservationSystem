namespace AirlineReservation.Application.Common.Interfaces;

/// <summary>
/// Small pure input model for calculating a single seat's server-side price without EF entities.
/// </summary>
public sealed record SeatPricingInput(
    decimal FlightBasePrice,
    decimal SeatMultiplier,
    bool IsWindow,
    bool IsAisle,
    DateTime DepartureTimeUtc);

/// <summary>
/// Itemized server-side price breakdown for a single seat.
/// </summary>
public sealed record SeatPriceBreakdown(
    decimal FlightBasePrice,
    decimal SeatMultiplier,
    decimal BaseSeatPrice,
    decimal PositionFee,
    decimal DemandMultiplier,
    bool IsLastMinuteDemandApplied,
    decimal FinalPrice);

/// <summary>
/// Single source of truth for all server-side seat price calculations (seat map, quote, checkout).
/// </summary>
public interface IPricingService
{
    SeatPriceBreakdown CalculateSeatPrice(SeatPricingInput input);
}
