using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.Common.Models;

namespace AirlineReservation.Application.Common.Services;

/// <summary>
/// Deterministic server-side pricing engine using configurable fees and injected TimeProvider.
/// Formula per seat: round((flightBasePrice * seatMultiplier + positionFee) * demandMultiplier, 2, AwayFromZero).
/// </summary>
public sealed class PricingService : IPricingService
{
    private readonly PricingSettings _settings;
    private readonly TimeProvider _timeProvider;

    public PricingService(PricingSettings settings, TimeProvider timeProvider)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        if (!_settings.IsValid(out var error))
        {
            throw new ArgumentException(error, nameof(settings));
        }
    }

    public SeatPriceBreakdown CalculateSeatPrice(SeatPricingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.FlightBasePrice < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Flight base price cannot be negative.");
        }

        decimal effectiveMultiplier = input.SeatMultiplier > 0m ? input.SeatMultiplier : 1.0m;
        decimal baseSeatPrice = input.FlightBasePrice * effectiveMultiplier;

        decimal positionFee = input.IsWindow
            ? _settings.WindowFee
            : input.IsAisle
                ? _settings.AisleFee
                : 0m;

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var departureUtc = DateTime.SpecifyKind(input.DepartureTimeUtc, DateTimeKind.Utc);
        var timeUntilDeparture = departureUtc - utcNow;
        var demandWindow = TimeSpan.FromHours(_settings.LastMinuteDays * 24d);

        bool isLastMinuteDemandApplied = timeUntilDeparture <= demandWindow;
        decimal demandMultiplier = isLastMinuteDemandApplied ? _settings.LastMinuteMultiplier : 1.0m;

        decimal rawTotal = (baseSeatPrice + positionFee) * demandMultiplier;
        decimal finalPrice = Math.Round(rawTotal, 2, MidpointRounding.AwayFromZero);

        return new SeatPriceBreakdown(
            FlightBasePrice: input.FlightBasePrice,
            SeatMultiplier: effectiveMultiplier,
            BaseSeatPrice: Math.Round(baseSeatPrice, 2, MidpointRounding.AwayFromZero),
            PositionFee: positionFee,
            DemandMultiplier: demandMultiplier,
            IsLastMinuteDemandApplied: isLastMinuteDemandApplied,
            FinalPrice: finalPrice);
    }
}
