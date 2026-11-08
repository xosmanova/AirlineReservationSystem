namespace AirlineReservation.Application.Common.Interfaces;

/// <summary>
/// Provides unified, timezone-aware business time calculations.
/// Operates on the configured business time zone (e.g. "Asia/Baku") using TimeProvider.
/// Ensures all queries, booking expiries, and cancellation windows share consistent day boundaries.
/// </summary>
public interface IBusinessTime
{
    /// <summary>
    /// Gets today's calendar date in the business local time zone (e.g. 2026-11-10 00:00:00).
    /// </summary>
    DateTime TodayLocal { get; }

    /// <summary>
    /// Converts a UTC DateTime to the business local time zone.
    /// </summary>
    DateTime ToLocal(DateTime utcDateTime);

    /// <summary>
    /// Computes the exact UTC start boundary for a given local calendar date.
    /// E.g. For Baku (UTC+4), 2026-11-10 local day starts at 2026-11-09 20:00:00 UTC.
    /// </summary>
    DateTime StartOfLocalDayUtc(DateTime localDate);

    /// <summary>
    /// Computes the exact UTC end boundary for a given local calendar date (exclusive).
    /// E.g. For Baku (UTC+4), 2026-11-10 local day ends at 2026-11-10 20:00:00 UTC.
    /// </summary>
    DateTime EndOfLocalDayUtc(DateTime localDate);

    /// <summary>
    /// Gets the resolved TimeZoneInfo for the business timezone.
    /// </summary>
    TimeZoneInfo BusinessTimeZone { get; }
}
