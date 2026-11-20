using AirlineReservation.Application.Common.Helpers;
using AirlineReservation.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace AirlineReservation.Application.Common.Services;

/// <summary>
/// Implementation of IBusinessTime powered by TimeProvider and configured business time zone.
/// </summary>
public class BusinessTimeService : IBusinessTime
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;

    public BusinessTimeService(TimeProvider timeProvider, IConfiguration configuration)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        var timeZoneId = configuration["Business:TimeZone"] ?? "Asia/Baku";
        _timeZone = TimeZoneHelper.ResolveTimeZone(timeZoneId);
    }

    public BusinessTimeService(TimeProvider timeProvider, TimeZoneInfo timeZone)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _timeZone = timeZone ?? throw new ArgumentNullException(nameof(timeZone));
    }

    public DateTime TodayLocal
    {
        get
        {
            var localDateTime = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _timeZone);
            return localDateTime.Date;
        }
    }

    public TimeZoneInfo BusinessTimeZone => _timeZone;

    public DateTime ToLocal(DateTime utcDateTime)
    {
        var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, _timeZone);
    }

    public DateTime StartOfLocalDayUtc(DateTime localDate)
    {
        var localStart = new DateTime(localDate.Year, localDate.Month, localDate.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var offset = _timeZone.GetUtcOffset(localStart);
        return DateTime.SpecifyKind(localStart - offset, DateTimeKind.Utc);
    }

    public DateTime EndOfLocalDayUtc(DateTime localDate)
    {
        return StartOfLocalDayUtc(localDate).AddDays(1);
    }
}
