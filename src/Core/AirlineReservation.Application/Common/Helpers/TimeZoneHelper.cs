namespace AirlineReservation.Application.Common.Helpers;

public static class TimeZoneHelper
{
    /// <summary>
    /// Resolves an IANA or Windows time zone ID safely across operating systems.
    /// E.g. "Asia/Baku" is resolved directly or mapped to "Azerbaijan Standard Time" on Windows.
    /// </summary>
    public static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
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
            return TimeZoneInfo.Utc;
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
