using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Common.Services;

namespace AirlineReservation.Application.Tests.Services;

public class PricingServiceTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static PricingService CreateService(PricingSettings? settings = null, DateTimeOffset? now = null)
        => new(settings ?? new PricingSettings(), new FakeClock(now ?? FixedUtcNow));

    [Theory]
    [InlineData(100.00, 1.0, false, false, 10, 100.00, 0.00, 1.0, false, 100.00)] // Economy Middle, >72h
    [InlineData(100.00, 1.0, true, false, 10, 100.00, 10.00, 1.0, false, 110.00)] // Economy Window, >72h
    [InlineData(100.00, 1.0, false, true, 10, 100.00, 5.00, 1.0, false, 105.00)]  // Economy Aisle, >72h
    [InlineData(100.00, 1.0, true, true, 10, 100.00, 10.00, 1.0, false, 110.00)]  // Window + Aisle -> WindowFee wins
    [InlineData(100.00, 1.5, true, false, 10, 150.00, 10.00, 1.0, false, 160.00)] // ExitRow Window, >72h
    [InlineData(100.00, 2.0, true, false, 10, 200.00, 10.00, 1.0, false, 210.00)] // Business Window, >72h
    [InlineData(100.00, 2.0, false, true, 2, 200.00, 5.00, 1.2, true, 246.00)]    // Business Aisle, <=72h ((200+5)*1.2)
    [InlineData(100.00, 1.0, false, false, 2, 100.00, 0.00, 1.2, true, 120.00)]   // Economy Middle, <=72h
    [InlineData(100.00, 1.0, true, false, 2, 100.00, 10.00, 1.2, true, 132.00)]   // Economy Window, <=72h ((100+10)*1.2)
    [InlineData(100.00, 1.5, false, true, 2, 150.00, 5.00, 1.2, true, 186.00)]    // ExitRow Aisle, <=72h ((150+5)*1.2)
    public void CalculateSeatPrice_ReturnsExpectedBreakdown_AcrossSeatTypesAndDemandWindows(
        decimal basePrice,
        decimal seatMultiplier,
        bool isWindow,
        bool isAisle,
        int daysUntilDeparture,
        decimal expectedBaseSeatPrice,
        decimal expectedPositionFee,
        decimal expectedDemandMultiplier,
        bool expectedLastMinute,
        decimal expectedFinalPrice)
    {
        var sut = CreateService();
        var departureUtc = FixedUtcNow.UtcDateTime.AddDays(daysUntilDeparture);

        var result = sut.CalculateSeatPrice(new SeatPricingInput(
            basePrice,
            seatMultiplier,
            isWindow,
            isAisle,
            departureUtc));

        Assert.Equal(expectedBaseSeatPrice, result.BaseSeatPrice);
        Assert.Equal(expectedPositionFee, result.PositionFee);
        Assert.Equal(expectedDemandMultiplier, result.DemandMultiplier);
        Assert.Equal(expectedLastMinute, result.IsLastMinuteDemandApplied);
        Assert.Equal(expectedFinalPrice, result.FinalPrice);
    }

    [Fact]
    public void CalculateSeatPrice_DemandMultiplierBoundary_IsInclusiveAt72HoursAndExclusiveAt72Hours01Minute()
    {
        var sut = CreateService();
        var exact72h = FixedUtcNow.UtcDateTime.AddHours(72);
        var justPast72h = FixedUtcNow.UtcDateTime.AddHours(72).AddMinutes(1);

        var atBoundary = sut.CalculateSeatPrice(new SeatPricingInput(100.00m, 1.0m, true, false, exact72h));
        var pastBoundary = sut.CalculateSeatPrice(new SeatPricingInput(100.00m, 1.0m, true, false, justPast72h));

        Assert.True(atBoundary.IsLastMinuteDemandApplied);
        Assert.Equal(1.20m, atBoundary.DemandMultiplier);
        Assert.Equal(132.00m, atBoundary.FinalPrice);

        Assert.False(pastBoundary.IsLastMinuteDemandApplied);
        Assert.Equal(1.0m, pastBoundary.DemandMultiplier);
        Assert.Equal(110.00m, pastBoundary.FinalPrice);
    }

    [Fact]
    public void CalculateSeatPrice_UsesMidpointRoundingAwayFromZero_ForHalfCentTotals()
    {
        // 10.02 * 1.25 = 12.525 -> AwayFromZero rounds to 12.53 (ToEven would round to 12.52)
        var settings = new PricingSettings
        {
            WindowFee = 0m,
            AisleFee = 0m,
            LastMinuteDays = 3,
            LastMinuteMultiplier = 1.25m
        };
        var sut = CreateService(settings);

        var result = sut.CalculateSeatPrice(new SeatPricingInput(
            10.02m,
            1.0m,
            false,
            false,
            FixedUtcNow.UtcDateTime.AddHours(24)));

        Assert.Equal(12.53m, result.FinalPrice);
    }

    [Theory]
    [InlineData(-0.01, 5.00, 3, 1.20)]
    [InlineData(10.00, -1.00, 3, 1.20)]
    [InlineData(10.00, 5.00, -1, 1.20)]
    [InlineData(10.00, 5.00, 3, 0.99)]
    public void PricingSettings_RejectsInvalidValues_AndConstructorThrows(
        decimal windowFee,
        decimal aisleFee,
        int lastMinuteDays,
        decimal lastMinuteMultiplier)
    {
        var invalid = new PricingSettings
        {
            WindowFee = windowFee,
            AisleFee = aisleFee,
            LastMinuteDays = lastMinuteDays,
            LastMinuteMultiplier = lastMinuteMultiplier
        };

        Assert.False(invalid.IsValid(out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.Throws<ArgumentException>(() => new PricingService(invalid, new FakeClock(FixedUtcNow)));
    }

    [Fact]
    public void PricingSettings_AcceptsZeroFeesZeroDaysAndUnitMultiplier()
    {
        var zeroBoundary = new PricingSettings
        {
            WindowFee = 0m,
            AisleFee = 0m,
            LastMinuteDays = 0,
            LastMinuteMultiplier = 1.0m
        };

        Assert.True(zeroBoundary.IsValid(out var error));
        Assert.Equal(string.Empty, error);

        var sut = CreateService(zeroBoundary);
        var exactNow = sut.CalculateSeatPrice(new SeatPricingInput(100m, 1.0m, true, false, FixedUtcNow.UtcDateTime));
        var future1m = sut.CalculateSeatPrice(new SeatPricingInput(100m, 1.0m, true, false, FixedUtcNow.UtcDateTime.AddMinutes(1)));

        Assert.True(exactNow.IsLastMinuteDemandApplied);
        Assert.False(future1m.IsLastMinuteDemandApplied);
        Assert.Equal(100.00m, exactNow.FinalPrice);
    }
}
