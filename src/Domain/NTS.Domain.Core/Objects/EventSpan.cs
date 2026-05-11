using System.Globalization;

namespace NTS.Domain.Core.Objects;

public record EventSpan
{
    public EventSpan(DateTimeOffset startDay, DateTimeOffset endDay)
    {
        StartDay = new DateTimeOffset(startDay.Year, startDay.Month, startDay.Day, 0, 0, 0, startDay.Offset);
        EndDay = new DateTimeOffset(endDay.Year, endDay.Month, endDay.Day, 23, 59, 59, endDay.Offset);
    }

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
}
