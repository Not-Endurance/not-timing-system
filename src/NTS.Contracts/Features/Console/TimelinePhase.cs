namespace NTS.Contracts.Features.Console;

/// <summary>A Phase of the timeline of a Participation (#619): its gate and the time events it recorded, in the order it recorded them.</summary>
public sealed class TimelinePhase
{
    public Guid Id { get; init; }
    public string Gate { get; init; } = string.Empty;
    public IReadOnlyList<TimelineEntry> Entries { get; init; } = [];
}
