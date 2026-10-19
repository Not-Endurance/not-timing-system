using NTS.Domain.Core.Aggregates;

namespace NTS.Contracts.Features.Participants;

/// <summary>
/// A Ranking that counts a Participation besides the one of its own Competition and Category, and what the Participation placed
/// as in it (#639, ADR-0006): none when the Ranking cannot be ranked.
/// </summary>
public sealed class CountedPlacing
{
    public CountedPlacing(string name, string category, Placing? placing)
    {
        Name = name;
        Category = category;
        Placing = placing;
    }

    public string Name { get; }
    public string Category { get; }
    public Placing? Placing { get; }
}

/// <summary>
/// A Participation in the history of an Athlete or a Horse, as <c>GET /api/participations/history</c> tells it (#638, #639,
/// ADR-0010): the Event that it was in and where, the Competition, the other half of the Combination, what it placed as in the
/// Ranking of its own Competition and Category, and the other Rankings that count it. The Participation itself, with its
/// Phases and totals, is read by its id.
/// </summary>
public sealed class HistoricParticipation
{
    public HistoricParticipation(
        Guid id,
        Guid eventId,
        string eventName,
        string location,
        DateTimeOffset startDay,
        string competition,
        ParticipantName other,
        Placing? placing,
        IReadOnlyList<CountedPlacing> alsoCountedIn
    )
    {
        Id = id;
        EventId = eventId;
        EventName = eventName;
        Location = location;
        StartDay = startDay;
        Competition = competition;
        Other = other;
        Placing = placing;
        AlsoCountedIn = alsoCountedIn;
    }

    /// <summary>The id of the Participation.</summary>
    public Guid Id { get; }

    public Guid EventId { get; }
    public string EventName { get; }
    public string Location { get; }
    public DateTimeOffset StartDay { get; }
    public string Competition { get; }

    /// <summary>The other half of the Combination: the Horse of an Athlete's Participation, the Athlete of a Horse's.</summary>
    public ParticipantName Other { get; }

    /// <summary>None when the Participation has no Ranking of its own Competition and Category, or the Ranking cannot be ranked.</summary>
    public Placing? Placing { get; }

    public IReadOnlyList<CountedPlacing> AlsoCountedIn { get; }
}

/// <summary>A page of a history, newest first, and whether there is a next one.</summary>
public sealed class ParticipationHistoryPage
{
    public ParticipationHistoryPage(IReadOnlyList<HistoricParticipation> items, bool hasMore)
    {
        Items = items;
        HasMore = hasMore;
    }

    public IReadOnlyList<HistoricParticipation> Items { get; }
    public bool HasMore { get; }
}
