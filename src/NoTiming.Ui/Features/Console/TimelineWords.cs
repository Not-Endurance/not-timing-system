using NTS.Contracts.Features.Console;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Events;
using NTS.Domain.Enums;

namespace NoTiming.Ui.Features.Console;

/// <summary>What the timeline of the Console calls a time event and how it was acquired (#619), in the words the person is told them in.</summary>
public static class TimelineWords
{
    /// <summary>
    /// The kind of time the event is for, and that it is an update of one when it is: "Arrive", "Present (update)"; an edit of
    /// the Phase form is told by its name, and its times by <see cref="TimesOf"/>.
    /// </summary>
    public static string KindOf(ParticipationEventBase timeEvent)
    {
        if (timeEvent is not SnapshotTimeEvent snapshot)
        {
            return Timeline_phase_edit_string;
        }

        var kind = LabelOf(snapshot.Slot);
        return snapshot is ArriveUpdated or PresentUpdated ? string.Format(Timeline_update_of__string, kind) : kind;
    }

    /// <summary>The times the event lists; an edit of the Phase form says which time each is, because it lists several.</summary>
    public static IReadOnlyList<TimelineTime> TimesOf(ParticipationEventBase timeEvent)
    {
        var isNamed = timeEvent is not SnapshotTimeEvent;
        return
        [
            .. timeEvent.Times.Select(x => new TimelineTime
            {
                Label = isNamed ? LabelOf(x.Slot) : null,
                Time = x.Time,
            }),
        ];
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
        return string.Join(
            ", ",
            ids.Where(events.ContainsKey).Select(x => $"{KindOf(events[x])} {string.Join(", ", TimesOf(events[x]))}")
        );
    }

    static string LabelOf(TimeSlot slot)
    {
        return slot switch
        {
            TimeSlot.Arrive => Arrive_string,
            TimeSlot.Represent => Represent_string,
            _ => Presentation_string,
        };
    }
}
