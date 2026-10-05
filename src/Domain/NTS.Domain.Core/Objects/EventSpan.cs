using System.Globalization;
using NTS.Domain.Access;

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

    /// <summary>
    /// Whether an Event with this span is Live at the instant: until the end of its last day, and Historic from that second
    /// on, with no grace (ADR-0007). It is the rule of <see cref="EventStageRule"/>, so the Api and the domain cannot differ.
    /// </summary>
    public bool IsLive(DateTimeOffset now)
    {
        return EventStageRule.Of(true, EndDay, now) == EventStage.Live;
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
