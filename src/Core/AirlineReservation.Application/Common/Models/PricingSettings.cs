namespace AirlineReservation.Application.Common.Models;

/// <summary>
/// Configurable seat pricing rules including position fees and last-minute demand multiplier.
/// Validated on application startup via ValidateOnStart.
/// </summary>
public sealed class PricingSettings
{
    public const string SectionName = "Pricing";

    public decimal WindowFee { get; set; } = 10.00m;
    public decimal AisleFee { get; set; } = 5.00m;
    public int LastMinuteDays { get; set; } = 3;
    public decimal LastMinuteMultiplier { get; set; } = 1.20m;

    public bool IsValid(out string error)
    {
        if (WindowFee < 0m)
        {
            error = "Pricing:WindowFee must be greater than or equal to 0.";
            return false;
        }

        if (AisleFee < 0m)
        {
            error = "Pricing:AisleFee must be greater than or equal to 0.";
            return false;
        }

        if (LastMinuteDays < 0)
        {
            error = "Pricing:LastMinuteDays must be greater than or equal to 0.";
            return false;
        }

        if (LastMinuteMultiplier < 1.0m)
        {
            error = "Pricing:LastMinuteMultiplier must be greater than or equal to 1.0.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
