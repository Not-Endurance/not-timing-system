using NTS.Domain.Core.Aggregates.Participations.Entities;

namespace NTS.Contracts.Features.Console;

/// <summary>
/// A time event as the timeline of the Console shows it (#619): what kind of time it is, the time, what became of it, how it
/// was acquired, the name of the person who recorded it and when, when the Console Operator disabled it if it did, and whether it
/// is the time its Phase shows now. The Console Operator disables an event that was accepted and enables one that it disabled; what
/// the rules rejected is neither.
/// </summary>
public sealed class TimelineEntry
{
    public Guid Id { get; init; }
    public string Kind { get; init; } = string.Empty;

    /// <summary>The times the event lists: one for a Snapshot or an Update of one, up to three for an edit of the Phase form.</summary>
    public IReadOnlyList<TimelineTime> Times { get; init; } = [];
    public TimeEventOutcome Outcome { get; init; }
    public string OutcomeText { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;

    /// <summary>The name of the account that recorded the event; none for a time that was there before events were kept, and for an account that is not known.</summary>
    public string? Actor { get; init; }

    public DateTimeOffset? RecordedAt { get; init; }
    public DateTimeOffset? DisabledAt { get; init; }

    /// <summary>Whether the Phase shows this time: it is the latest accepted event of its kind.</summary>
    public bool IsShown { get; init; }

    /// <summary>Whether the Console Operator may disable the event: it was accepted.</summary>
    public bool CanDisable { get; init; }

    /// <summary>Whether the Console Operator may enable the event: it disabled it. What the rules rejected is neither.</summary>
    public bool CanEnable { get; init; }
}
