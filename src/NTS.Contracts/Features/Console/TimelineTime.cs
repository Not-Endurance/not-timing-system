using NTS.Domain.Objects;

namespace NTS.Contracts.Features.Console;

/// <summary>A time that an event of the timeline lists (#619, #620): which time it is when the event lists several, and the time, or none when the event cleared it.</summary>
public sealed class TimelineTime
{
    /// <summary>Which time of the Phase it is, told when the event lists several (an edit of the Phase form); none when the kind of the event says it.</summary>
    public string? Label { get; init; }

    /// <summary>The time the event gives; none when it cleared the time.</summary>
    public Timestamp? Time { get; init; }

    public override string ToString()
    {
        var time = Time?.ToString() ?? NTS.Localization.NtsStrings.Timeline_cleared_string;
        return Label == null ? time : $"{Label}: {time}";
    }
}
