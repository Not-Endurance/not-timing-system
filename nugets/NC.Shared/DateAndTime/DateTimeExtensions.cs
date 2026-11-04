using System.Reflection;

namespace Not.DateAndTime;

public static class DateTimeExtensions
{
    public static DateTimeOffset ToLocalDateTime(this DateTime time)
    {
        return DateTime.SpecifyKind(time, DateTimeKind.Local);
    }

    public static DateTimeOffset ToLocalDateTime(this DateTime date, TimeSpan timeSpan, TimeZoneInfo? timeZone = null)
    {
        var zone = timeZone ?? TimeZoneInfo.Local;
        var localDateTime = DateTime.SpecifyKind(date.Date.Add(timeSpan), DateTimeKind.Unspecified);
        return new DateTimeOffset(localDateTime, zone.GetUtcOffset(localDateTime));
    }

    public static DateTime ToTodayDateTime(this TimeSpan timeSpan)
    {
        return DateTime.Today.Date.Add(timeSpan);
    }
}
