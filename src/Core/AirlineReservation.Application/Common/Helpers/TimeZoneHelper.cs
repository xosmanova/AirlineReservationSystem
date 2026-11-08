namespace AirlineReservation.Application.Common.Helpers;

public static class TimeZoneHelper
{
    /// <summary>
    /// Resolves an IANA or Windows time zone ID safely across operating systems.
    /// E.g. "Asia/Baku" is resolved directly on Linux/macOS or mapped to "Azerbaijan Standard Time" on Windows.
    /// Fails fast if the time zone cannot be resolved (no silent UTC fallback).
    /// </summary>
    public static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            throw new ArgumentException("Time zone ID cannot be null or empty.", nameof(timeZoneId));
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out var windowsId))
            {
                return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
            }

            throw new InvalidOperationException(
                $"Business time zone '{timeZoneId}' could not be resolved on this operating system. Please check configuration.");
        }
    }

    /// <summary>
    /// Computes today's date in the specified time zone using TimeProvider.
    /// </summary>
    public static DateTime GetTodayInTimeZone(TimeProvider timeProvider, TimeZoneInfo timeZone)
    {
        var localDateTime = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone);
        return localDateTime.Date;
    }
}
