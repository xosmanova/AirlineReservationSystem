using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.Common.Services;
using AirlineReservation.Application.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AirlineReservation.Application.Tests.Services;

public class CancellationPolicyServiceTests
{
    private readonly CancellationPolicyService _sut = new();
    private static readonly DateTime FixedNowUtc = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(24.01)]  // 24 hours and 36 seconds
    [InlineData(25.0)]   // 25 hours
    [InlineData(48.0)]   // 2 days
    [InlineData(168.0)]  // 7 days
    public void CalculateRefund_MoreThan24HoursBeforeDeparture_Returns100PercentRefund(double hoursAhead)
    {
        var departureTimeUtc = FixedNowUtc.AddHours(hoursAhead);
        decimal totalAmount = 200.00m;

        var result = _sut.CalculateRefund(totalAmount, departureTimeUtc, FixedNowUtc);

        Assert.True(result.CanCancel);
        Assert.Equal(100m, result.RefundPercentage);
        Assert.Equal(200.00m, result.RefundAmount);
        Assert.Equal(0m, result.PenaltyAmount);
        Assert.Contains("100%", result.Reason);
    }

    [Fact]
    public void CalculateRefund_Exactly24HoursBeforeDeparture_Returns50PercentRefund()
    {
        var departureTimeUtc = FixedNowUtc.AddHours(24);
        decimal totalAmount = 150.00m;

        var result = _sut.CalculateRefund(totalAmount, departureTimeUtc, FixedNowUtc);

        Assert.True(result.CanCancel);
        Assert.Equal(50m, result.RefundPercentage);
        Assert.Equal(75.00m, result.RefundAmount);
        Assert.Equal(75.00m, result.PenaltyAmount);
        Assert.Contains("50%", result.Reason);
    }

    [Theory]
    [InlineData(23.99)]  // 23 hours 59 minutes
    [InlineData(12.0)]   // 12 hours
    [InlineData(2.0)]    // 2 hours
    [InlineData(0.01)]   // 36 seconds before departure
    public void CalculateRefund_LessThan24HoursBeforeDeparture_Returns50PercentRefund(double hoursAhead)
    {
        var departureTimeUtc = FixedNowUtc.AddHours(hoursAhead);
        decimal totalAmount = 300.00m;

        var result = _sut.CalculateRefund(totalAmount, departureTimeUtc, FixedNowUtc);

        Assert.True(result.CanCancel);
        Assert.Equal(50m, result.RefundPercentage);
        Assert.Equal(150.00m, result.RefundAmount);
        Assert.Equal(150.00m, result.PenaltyAmount);
        Assert.Contains("50%", result.Reason);
    }

    [Fact]
    public void CalculateRefund_ExactDepartureTime_ReturnsDisallowed()
    {
        var departureTimeUtc = FixedNowUtc;
        decimal totalAmount = 250.00m;

        var result = _sut.CalculateRefund(totalAmount, departureTimeUtc, FixedNowUtc);

        Assert.False(result.CanCancel);
        Assert.Equal(0m, result.RefundPercentage);
        Assert.Equal(0m, result.RefundAmount);
        Assert.Equal(250.00m, result.PenaltyAmount);
        Assert.Contains("passed", result.Reason);
    }

    [Fact]
    public void CalculateRefund_PastDepartureTime_ReturnsDisallowed()
    {
        var departureTimeUtc = FixedNowUtc.AddHours(-2);
        decimal totalAmount = 180.00m;

        var result = _sut.CalculateRefund(totalAmount, departureTimeUtc, FixedNowUtc);

        Assert.False(result.CanCancel);
        Assert.Equal(0m, result.RefundPercentage);
        Assert.Equal(0m, result.RefundAmount);
        Assert.Equal(180.00m, result.PenaltyAmount);
    }

    [Fact]
    public void CalculateRefund_OddAmountFiftyPercent_RoundsCentAccuratelyAndSumsToTotal()
    {
        var departureTimeUtc = FixedNowUtc.AddHours(10);
        decimal totalAmount = 125.75m;

        var result = _sut.CalculateRefund(totalAmount, departureTimeUtc, FixedNowUtc);

        Assert.True(result.CanCancel);
        Assert.Equal(62.88m, result.RefundAmount);
        Assert.Equal(62.87m, result.PenaltyAmount);
        Assert.Equal(totalAmount, result.RefundAmount + result.PenaltyAmount);
    }

    [Fact]
    public void CalculateRefund_NegativeAmount_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _sut.CalculateRefund(-100m, FixedNowUtc.AddDays(1), FixedNowUtc));
    }

    [Fact]
    public void ServiceRegistration_RegistersCancellationPolicyServiceAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICancellationPolicyService));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(CancellationPolicyService), descriptor.ImplementationType);
    }
}
