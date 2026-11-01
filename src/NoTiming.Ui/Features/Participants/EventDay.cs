using NTS.Domain.Core.Objects;

namespace NoTiming.Ui.Features.Participants;

/// <summary>
/// The day an Event starts on, as the day of its place (#639, ADR-0010). An Event is stored as instants, the first second of its
/// first day and the last second of its last wherever it is, and the Api tells instants in UTC, so the date of the instant is
/// the day before for a place that is ahead of UTC. The domain says the day (<see cref="EventSpan.DayOfFirstSecond"/>): the same
/// for every viewer, whatever zone their browser is in.
/// </summary>
public static class EventDay
{
    public static DateTime Of(DateTimeOffset firstSecond)
    {
        return EventSpan.DayOfFirstSecond(firstSecond).ToDateTime(TimeOnly.MinValue);
    }
}
