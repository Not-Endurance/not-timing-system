using System.Reflection;

namespace Not.DateAndTime;

public static class DateTimeExtensions
{
    public static DateTimeOffset ToLocalDateTime(this DateTime time)
    {
        return DateTime.SpecifyKind(time, DateTimeKind.Local);
    }

    public static DateTime ToTodayDateTime(this TimeSpan timeSpan)
    {
        return DateTime.Today.Date.Add(timeSpan);
    }
}
