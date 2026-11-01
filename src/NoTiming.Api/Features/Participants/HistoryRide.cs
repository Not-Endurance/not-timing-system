namespace NoTiming.Api.Features.Participants;

/// <summary>
/// A ride in the history of an Athlete or a Horse as the database tells it (#638, ADR-0010): a Participation joined with the Event it
/// was ridden in, with only the members that stand for one value, which a list can be filtered and sorted on. The members
/// are the ones the resource shows as they are stored, in camelCase; what is composed from other documents (the other half of
/// the Combination, the placing, the other Rankings that count the ride) is not here, and so cannot be asked for.
/// </summary>
internal sealed class HistoryRide
{
    /// <summary>The members of a ride that a list cannot be filtered or sorted on: what the database needs to know to leave a ride out.</summary>
    public static IReadOnlyCollection<string> Hidden { get; } = [nameof(EventIsDeleted)];

    /// <summary>The id of the Participation.</summary>
    public Guid Id { get; set; }

    public Guid EventId { get; set; }
    public string EventName { get; set; } = default!;
    public string Location { get; set; } = default!;
    public DateTimeOffset StartDay { get; set; }
    public DateTimeOffset EndDay { get; set; }
    public string Competition { get; set; } = default!;
    public string Ruleset { get; set; } = default!;
    public string Category { get; set; } = default!;
    public Guid AthleteId { get; set; }
    public Guid HorseId { get; set; }

    /// <summary>Whether the Functions host flagged the Event as deleted.</summary>
    public bool EventIsDeleted { get; set; }
}
