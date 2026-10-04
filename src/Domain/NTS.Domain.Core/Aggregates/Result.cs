using NTS.Domain.Core.Aggregates.Results;
using NTS.Domain.Core.Objects.Rankers;

namespace NTS.Domain.Core.Aggregates;

/// <summary>
/// What a Ranking or a Handout shows, composed in memory from the Participations it names whenever it is needed
/// (ADR-0006). It is a read model: it is never stored, and the next composition shows the Participations as they are
/// by then.
/// </summary>
public sealed class Result
{
    static readonly FeiRanker FEI_RANKER = new();
    static readonly Ranker[] REGIONAL_RANKERS = [];

    Result(
        Guid id,
        Guid? rankingId,
        string name,
        CompetitionRuleset ruleset,
        ParticipationCategory category,
        Guid eventId,
        List<ParticipationResult> entries,
        RegionalRules? rules,
        IReadOnlyList<Ranker>? regionalRankers
    )
    {
        Id = id;
        EventId = eventId;
        RankingId = rankingId;
        Name = name;
        Ruleset = ruleset;
        Category = category;
        Entries = RankIfRequired(entries, ruleset, rules ?? RegionalRules.None, regionalRankers ?? REGIONAL_RANKERS)
            .AsReadOnly();
    }

    /// <summary>
    /// The Results of a Ranking, over the Participations of its Event, ranked by the rules of that Event (ADR-0012) with
    /// the regional rankers given. There are none yet, so this is how a test can show the rules to be read.
    /// </summary>
    internal Result(
        Ranking ranking,
        IEnumerable<Participation> participations,
        RegionalRules? rules,
        IReadOnlyList<Ranker>? regionalRankers
    )
        : this(
            ranking.Id,
            ranking.Id,
            ranking.Name,
            ranking.Ruleset,
            ranking.Category,
            ranking.EventId,
            Compose(ranking, participations),
            rules,
            regionalRankers
        ) { }

    /// <summary>
    /// The Results of a Ranking, over the Participations of its Event, ranked by the rules of that Event (ADR-0012).
    /// Composed without rules, they are ranked as by an Event that has none.
    /// </summary>
    public Result(Ranking ranking, IEnumerable<Participation> participations, RegionalRules? rules = null)
        : this(ranking, participations, rules, null) { }

    /// <summary>The sheet of a Handout, over the Participation it names.</summary>
    public Result(Handout handout, Participation participation)
        : this(
            handout.Id,
            null,
            participation.Competition.Name,
            participation.Competition.Ruleset,
            participation.Category,
            handout.EventId,
            [new ParticipationResult(NamedBy(handout, participation))],
            null,
            null
        ) { }

    public Guid Id { get; }
    public Guid EventId { get; }
    public Guid? RankingId { get; }
    public string Name { get; }
    public CompetitionRuleset Ruleset { get; }
    public ParticipationCategory Category { get; }
    public IReadOnlyList<ParticipationResult> Entries { get; }
    public bool IsRanked => Entries.Count > 1;
    public string Title => $"{Category}: {Name}";

    public override string ToString()
    {
        return $"{Name} {Category}: {Entries.Count}";
    }

    static List<ParticipationResult> Compose(Ranking ranking, IEnumerable<Participation> participations)
    {
        var byId = participations.DistinctBy(x => x.Id).ToDictionary(x => x.Id);
        return ranking
            .Entries.Select(entry => new ParticipationResult(
                Find(byId, ranking, entry.ParticipationId),
                entry.IsNotRanked,
                entry.Rank
            ))
            .ToList();
    }

    static Participation Find(Dictionary<Guid, Participation> participations, Ranking ranking, Guid id)
    {
        if (participations.TryGetValue(id, out var participation))
        {
            return participation;
        }

        throw GuardHelper.Exception(
            $"Ranking {ranking.Id} counts Participation {id}, which is not there to compose its Results."
        );
    }

    static Participation NamedBy(Handout handout, Participation participation)
    {
        if (handout.ParticipationId != participation.Id)
        {
            throw GuardHelper.Exception(
                $"Handout {handout.Id} is of Participation {handout.ParticipationId}, not of {participation.Id}."
            );
        }

        return participation;
    }

    static List<ParticipationResult> RankIfRequired(
        List<ParticipationResult> entries,
        CompetitionRuleset ruleset,
        RegionalRules rules,
        IReadOnlyList<Ranker> regionalRankers
    )
    {
        return entries.Count > 1 ? Rank(entries, ruleset, rules, regionalRankers) : entries;
    }

    static List<ParticipationResult> Rank(
        IReadOnlyCollection<ParticipationResult> entries,
        CompetitionRuleset ruleset,
        RegionalRules rules,
        IReadOnlyList<Ranker> regionalRankers
    )
    {
        var ranker = GetRanker(rules.RankerFor(ruleset), regionalRankers);
        var ranked = ranker.Rank(entries);
        var rank = 0;
        foreach (var entry in ranked)
        {
            entry.Rank = ++rank;
        }
        return ranked;
    }

    /// <summary>The regional ranker of the code the rules chose, and the FEI ranker when none was chosen or none has that code.</summary>
    static Ranker GetRanker(string? code, IReadOnlyList<Ranker> regionalRankers)
    {
        return code == null ? FEI_RANKER : regionalRankers.FirstOrDefault(x => x.CountryIsoCode == code) ?? FEI_RANKER;
    }
}
