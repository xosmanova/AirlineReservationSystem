namespace AirlineReservation.Application.Common.Helpers;

public static class TimeZoneHelper
{
    /// <summary>
    /// Resolves an IANA or Windows time zone ID safely across operating systems.
    /// E.g. "Asia/Baku" is resolved directly on Linux/macOS or mapped to "Azerbaijan Standard Time" on Windows.
    /// THROWS InvalidOperationException when the zone cannot be resolved (no silent UTC fallback).
    /// </summary>
    public static TimeZoneInfo GetRequiredTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            throw new InvalidOperationException("Business time zone configuration cannot be null or empty.");
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
    /// Backwards-compatible alias for GetRequiredTimeZone.
    /// </summary>
    public static TimeZoneInfo ResolveTimeZone(string timeZoneId) => GetRequiredTimeZone(timeZoneId);

    /// <summary>
    /// Computes today's date in the specified time zone using TimeProvider.
    /// </summary>
    public static DateTime GetTodayInTimeZone(TimeProvider timeProvider, TimeZoneInfo timeZone)
    {
        var localDateTime = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone);
        return localDateTime.Date;
    }
}
