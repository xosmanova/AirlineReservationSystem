using AirlineReservation.Application.Common.Interfaces;

namespace AirlineReservation.Application.Common.Services;

/// <summary>
/// Domain policy service enforcing tiered refund calculations:
/// - Greater than 24 hours: 100% refund, 0 penalty
/// - Within 24 hours (less than or equal to 24h): 50% refund, 50% penalty
/// - Past or departure reached (less than or equal to 0h): Disallowed
/// </summary>
public class CancellationPolicyService : ICancellationPolicyService
{
    public CancellationCalculationResult CalculateRefund(decimal totalAmount, DateTime departureTimeUtc, DateTime nowUtc)
    {
        if (totalAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(totalAmount), "Total amount cannot be negative.");
        }

        var depUtc = DateTime.SpecifyKind(departureTimeUtc, DateTimeKind.Utc);
        var currentUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);

        var timeUntilDeparture = depUtc - currentUtc;

        // Rule 3: Flight has departed or departure time is reached -> Cancellation disallowed
        if (timeUntilDeparture <= TimeSpan.Zero)
        {
            return new CancellationCalculationResult(
                CanCancel: false,
                RefundPercentage: 0m,
                RefundAmount: 0m,
                PenaltyAmount: totalAmount,
                Reason: "Flight has already departed or departure time has passed. Cancellation is no longer permitted.");
        }

        // Rule 1: More than 24 hours before departure -> 100% full refund
        if (timeUntilDeparture > TimeSpan.FromHours(24))
        {
            return new CancellationCalculationResult(
                CanCancel: true,
                RefundPercentage: 100m,
                RefundAmount: totalAmount,
                PenaltyAmount: 0m,
                Reason: "Cancelled more than 24 hours before departure: Full refund (100%) granted.");
        }

        // Rule 2: 24 hours or less before departure -> 50% refund, 50% penalty fee
        var refundAmount = Math.Round(totalAmount * 0.50m, 2, MidpointRounding.AwayFromZero);
        var penaltyAmount = totalAmount - refundAmount;

        return new CancellationCalculationResult(
            CanCancel: true,
            RefundPercentage: 50m,
            RefundAmount: refundAmount,
            PenaltyAmount: penaltyAmount,
            Reason: "Cancelled 24 hours or less before departure: 50% cancellation fee applied.");
    }
}
