namespace AirlineReservation.Application.Common.Interfaces;

/// <summary>
/// Breakdown calculation result for booking cancellation and refund eligibility.
/// </summary>
public sealed record CancellationCalculationResult(
    bool CanCancel,
    decimal RefundPercentage,
    decimal RefundAmount,
    decimal PenaltyAmount,
    string? Reason);

/// <summary>
/// Policy evaluator determining refund amounts and cancellation penalties based on departure lead time.
/// Rules:
/// - More than 24 hours until departure: 100% full refund, 0% penalty.
/// - 24 hours or less until departure (before departure): 50% refund, 50% penalty.
/// - Departure time reached or in past: 0% refund, cancellation disallowed.
/// </summary>
public interface ICancellationPolicyService
{
    CancellationCalculationResult CalculateRefund(decimal totalAmount, DateTime departureTimeUtc, DateTime nowUtc);
}
