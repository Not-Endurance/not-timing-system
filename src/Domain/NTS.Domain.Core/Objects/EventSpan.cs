using System.Globalization;
using NTS.Domain.Access;

namespace NTS.Domain.Core.Objects;

public record EventSpan
{
    /// <summary>
    /// The span of an Event as it is stored: the first second of its first day and the last second of its last, at its place, kept
    /// as the instants they are. The database and the Api tell instants in UTC, and making the days whole again in UTC would move
    /// them for a place ahead of UTC (its first day a day early, its end hours late), so an Event that is read is kept as it was made.
    /// </summary>
    public static EventSpan Stored(DateTimeOffset firstSecond, DateTimeOffset lastSecond)
    {
        return new EventSpan(firstSecond, lastSecond, kept: true);
    }

    /// <summary>
    /// The calendar day at the place of an Event of the first second of its first day. Half a day on from that second, in UTC, is
    /// inside the right day for every place from UTC-11 to UTC+12, so the day is the same for every viewer, whatever zone their
    /// browser is in. (A place at UTC-12 or beyond UTC+12 has no day by this rule.)
    /// </summary>
    public static DateOnly DayOfFirstSecond(DateTimeOffset firstSecond)
    {
        return DateOnly.FromDateTime(firstSecond.UtcDateTime.AddHours(12));
    }

    /// <summary>
    /// The calendar day at the place of an Event of the last second of its last day: half a day before it, in UTC, is inside the
    /// right day for every place from UTC-12 to UTC+11.
    /// </summary>
    public static DateOnly DayOfLastSecond(DateTimeOffset lastSecond)
    {
        return DateOnly.FromDateTime(lastSecond.UtcDateTime.AddHours(-12));
    }

    EventSpan(DateTimeOffset firstSecond, DateTimeOffset lastSecond, bool kept)
    {
        StartDay = firstSecond;
        EndDay = lastSecond;
    }

    public EventSpan(DateTimeOffset startDay, DateTimeOffset endDay)
    {
        StartDay = new DateTimeOffset(startDay.Year, startDay.Month, startDay.Day, 0, 0, 0, startDay.Offset);
        EndDay = new DateTimeOffset(endDay.Year, endDay.Month, endDay.Day, 23, 59, 59, endDay.Offset);
    }

    public DateTimeOffset StartDay { get; }
    public DateTimeOffset EndDay { get; }

    /// <summary>The calendar day the Event starts on at its place, the same for every viewer (<see cref="DayOfFirstSecond"/>).</summary>
    public DateOnly FirstDay => DayOfFirstSecond(StartDay);

    /// <summary>The calendar day the Event ends on at its place, the same for every viewer (<see cref="DayOfLastSecond"/>).</summary>
    public DateOnly LastDay => DayOfLastSecond(EndDay);

    /// <summary>
    /// Whether an Event with this span is Live at the instant: until the end of its last day, and Historic from that second
    /// on, with no grace (ADR-0007). It is the rule of <see cref="EventStageRule"/>, so the Api and the domain cannot differ.
    /// </summary>
    public bool IsLive(DateTimeOffset now)
    {
        return EventStageRule.Of(true, EndDay, now) == EventStage.Live;
    }

    /// <summary>
    /// The day to show for the Event at the instant, in the format of the culture: its first day before it starts, the day it is at
    /// its place while it runs (the whole days since its first second, from its first day), and its last day from the second it ends.
    /// The days are those of its place, whatever zone the viewer is in.
    /// </summary>
    public string Display(DateTimeOffset now)
    {
        DateOnly day;
        if (now < StartDay)
        {
            day = FirstDay;
        }
        else if (now < EndDay)
        {
            var passed = (int)Math.Floor((now - StartDay).TotalDays);
            day = FirstDay.AddDays(Math.Min(passed, LastDay.DayNumber - FirstDay.DayNumber));
        }
        else
        {
            day = LastDay;
        }

        return day.ToString("d", CultureInfo.CurrentCulture);
    }

    public override string ToString()
    {
        return Display(DateTimeOffset.Now);
    }
}
