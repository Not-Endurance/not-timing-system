using NTS.Domain.Core.Aggregates.Results;
using NTS.Domain.Core.Objects.Rankers;
using NTS.Domain.Core.StaticOptions;

namespace NTS.Domain.Core.Aggregates;

public class Result : Aggregate, IEventScoped
{
    static readonly FeiRanker FEI_RANKER = new();
    static readonly Ranker[] REGIONAL_RANKERS = [];

    protected Result(
        int? id,
        int? rankingId,
        string? name,
        CompetitionRuleset? ruleset,
        ParticipationCategory? category,
        IEnumerable<ParticipationResult> entries,
        int eventId
    )
        : base(id)
    {
        EventId = eventId;
        RankingId = rankingId;
        Name = Required(nameof(Name), name);
        Ruleset = Required(nameof(Ruleset), ruleset);
        Category = Required(nameof(Category), category);

        var participationResults = Required(nameof(Entries), entries).ToList();
        AreUnique(nameof(Participations), participationResults.Select(x => x.Participation)).ToList();
        Entries = RankIfRequired(participationResults, Ruleset).AsReadOnly();
    }

    public Result(Ranking ranking)
        : this(
            ranking.Id,
            ranking.Id,
            ranking.Name,
            ranking.Ruleset,
            ranking.Category,
            ranking.Entries.Select(ParticipationResult.From),
            ranking.EventId
        ) { }

    public int EventId { get; }
    public int? RankingId { get; }
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

    static List<ParticipationResult> RankIfRequired(
        List<ParticipationResult> entries,
        CompetitionRuleset ruleset
    )
    {
        return entries.Count > 1 ? Rank(entries, ruleset) : entries;
    }

    static List<ParticipationResult> Rank(
        IReadOnlyCollection<ParticipationResult> entries,
        CompetitionRuleset ruleset
    )
    {
        var ranker = StaticOption.ShouldUseRegionalRanker(ruleset)
            ? GetRanker(StaticOption.Regional)
            : FEI_RANKER;
        var ranked = ranker.Rank(entries);
        var rank = 0;
        foreach (var entry in ranked)
        {
            entry.Rank = ++rank;
        }
        return ranked;
    }

    static Ranker GetRanker(IRegionOption? configuration)
    {
        return REGIONAL_RANKERS.FirstOrDefault(x => x.CountryIsoCode == configuration?.CountryIsoCode) ?? FEI_RANKER;
    }
}
