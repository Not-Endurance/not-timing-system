using NoTiming.Api.Features.Tenancy;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Api.Features.Participants;

/// <summary>
/// A Ranking that counts a ride, other than the one of its own Competition and Category, and what the ride placed as in it
/// (#638, ADR-0006): a ride can be counted in several Rankings, with a mark of its own in each. The placing is left out when it
/// cannot be told, as a Ranking that cannot be ranked has none.
/// </summary>
internal sealed class RankingPlacing
{
    public RankingPlacing(Guid rankingId, string name, string ruleset, string category, Placing? placing)
    {
        RankingId = rankingId;
        Name = name;
        Ruleset = ruleset;
        Category = category;
        Placing = placing;
    }

    public Guid RankingId { get; }
    public string Name { get; }
    public string Ruleset { get; }
    public string Category { get; }
    public Placing? Placing { get; }
}

/// <summary>
/// One ride in the history of an Athlete or a Horse (#638, ADR-0010): the Event it was ridden in, the Competition and Category,
/// the other half of the Combination, and what the ride placed as. It is composed from the Participation, its Event and the
/// Rankings that count it whenever it is asked for, and never stored (ADR-0006). The members that a list can be filtered and
/// sorted on are the ones that stand for one value; the others are kept from the filter.
/// </summary>
internal sealed class ParticipationHistory
{
    /// <summary>The members that are objects or lists and so are not compared: a filter or a sort that names one is refused.</summary>
    public static IReadOnlyCollection<string> Hidden { get; } = [nameof(Other), nameof(Placing), nameof(AlsoCountedIn)];

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

    /// <summary>The other half of the Combination: the Horse of an Athlete's ride, the Athlete of a Horse's.</summary>
    public ParticipantName Other { get; set; } = default!;

    /// <summary>What the ride placed as in the Ranking of its own Competition and Category; none when there is no such Ranking or it cannot be ranked.</summary>
    public Placing? Placing { get; set; }

    public List<RankingPlacing> AlsoCountedIn { get; set; } = [];

    /// <summary>The members of the resource: everything but its id.</summary>
    public object Attributes()
    {
        return new
        {
            eventId = EventId,
            eventName = EventName,
            location = Location,
            startDay = StartDay,
            endDay = EndDay,
            competition = Competition,
            ruleset = Ruleset,
            category = Category,
            athleteId = AthleteId,
            horseId = HorseId,
            other = Other,
            placing = Placing,
            alsoCountedIn = AlsoCountedIn,
        };
    }
}
