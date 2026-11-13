namespace AirlineReservation.Application.Common.Models;

/// <summary>
/// Configurable seat hold settings including hold duration and background cleanup interval.
/// </summary>
public sealed class HoldSettings
{
    public const string SectionName = "Hold";

    public int Minutes { get; set; } = 10;
    public int CleanupSeconds { get; set; } = 30;
    public bool CleanupEnabled { get; set; } = true;

    public bool IsValid(out string error)
    {
        if (Minutes <= 0)
        {
            error = "Hold:Minutes must be greater than 0.";
            return false;
        }

        if (CleanupSeconds <= 0)
        {
            error = "Hold:CleanupSeconds must be greater than 0.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
