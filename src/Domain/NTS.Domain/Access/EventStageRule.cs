namespace NTS.Domain.Access;

/// <summary>
/// The rule that says where an Event is in its life (ADR-0007): not started until it starts, Live until the end of its
/// last day, Historic from then on, with no grace after it. Liveness is derived from the end and a clock, and is never a
/// stored flag, so it cannot lag behind the end. The end is an instant, the stored end of the last day (23:59:59 in the
/// Event's own offset), so it is the same whatever offset the clock is in.
/// </summary>
public static class EventStageRule
{
    /// <param name="started">Whether the Event has started, which is whether it has been made from its Setup.</param>
    /// <param name="end">The end of the Event's last day. A started Event that has none is taken to be over, so that
    /// nothing is written to a document that cannot say when it ends.</param>
    /// <param name="now">The clock of the host.</param>
    public static EventStage Of(bool started, DateTimeOffset? end, DateTimeOffset now)
    {
        if (!started)
        {
            return EventStage.Unstarted;
        }

        return end is { } last && now < last ? EventStage.Live : EventStage.Historic;
    }
}
