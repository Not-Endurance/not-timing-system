using System.Globalization;

namespace NoTiming.Domain.Core.Objects;

public record EventSpan
{
    public EventSpan(DateTimeOffset startDay, DateTimeOffset endDay, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        TimeZoneId = timeZoneId;
        StartDay = AtLocalTime(startDay, zone, 0, 0, 0);
        EndDay = AtLocalTime(endDay, zone, 23, 59, 59);
    }

    public string TimeZoneId { get; }
    public DateTimeOffset StartDay { get; }
    public DateTimeOffset EndDay { get; }

    public bool IsActive(DateTimeOffset now)
    {
        return now < EndDay;
    }

    public override string ToString()
    {
        var now = DateTimeOffset.Now;
        DateTimeOffset date;
        if (now > StartDay && now < EndDay)
        {
            date = now;
        }
        else if (StartDay > now)
        {
            date = StartDay;
        }
        else
        {
            date = EndDay;
        }
        return date.ToString("d", CultureInfo.CurrentCulture);
    }

    static DateTimeOffset AtLocalTime(DateTimeOffset instant, TimeZoneInfo zone, int hour, int minute, int second)
    {
        var date = TimeZoneInfo.ConvertTime(instant, zone).Date;
        var local = new DateTime(date.Year, date.Month, date.Day, hour, minute, second);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}
