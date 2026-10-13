using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Enums;

namespace NoTiming.Ui.Features.Console;

/// <summary>What the timeline of the Console calls a time event and how it was acquired (#619), in the words the person is told them in.</summary>
public static class TimelineWords
{
    /// <summary>The kind of time the event is for, and that it is an update of one when it is: "Arrive", "Present (update)".</summary>
    public static string KindOf(TimeEvent timeEvent)
    {
        var kind = timeEvent.Slot switch
        {
            TimeSlot.Arrive => Arrive_string,
            TimeSlot.Represent => Represent_string,
            _ => Presentation_string,
        };
        return timeEvent is ArriveUpdated or PresentUpdated ? string.Format(Timeline_update_of__string, kind) : kind;
    }

    public static string SourceOf(SnapshotMethod method)
    {
        return method switch
        {
            SnapshotMethod.Manual => Timeline_manual_string,
            SnapshotMethod.RFID => Timeline_rfid_string,
            SnapshotMethod.EmsIntegration => Timeline_ems_string,
            _ => method.ToString(),
        };
    }

    /// <summary>The events with these ids, each as the person sees it in the timeline: its kind and its time.</summary>
    public static string Describe(Participation participation, IEnumerable<Guid> ids)
    {
        var events = participation.Phases.SelectMany(x => x.Events).ToDictionary(x => x.Id);
        return string.Join(", ", ids.Where(events.ContainsKey).Select(x => $"{KindOf(events[x])} {events[x].Time}"));
    }
}
