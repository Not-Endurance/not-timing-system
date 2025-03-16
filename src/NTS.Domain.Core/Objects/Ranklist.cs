using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Rankers;
using NTS.Domain.Core.StaticOptions;

namespace NTS.Domain.Core.Objects;

public class Ranklist
{
    static FeiRanker _feiRanker = new();
    static Ranker[] _regionalRankers = [new BulgariaRanker()];

    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public Ranklist(Ranking ranking, IEnumerable<RankingEntry> entries)
    {
        Ranking = ranking;
        Entries = entries.ToList().AsReadOnly();
    }


    public Ranklist(Ranking ranking)
    {
        Entries = Rank(ranking);
        Ranking = ranking;
    }

    public IReadOnlyList<RankingEntry> Entries { get; private set; }
    public Ranking Ranking { get; }

    public int RankingId => Ranking.Id;
    public string Name => Ranking.Name;
    public AthleteCategory Category => Ranking.Category;
    public CompetitionType Type => Ranking.Type;
    public CompetitionRuleset Ruleset => Ranking.Ruleset;
    public string Title => $"{Category}: {Name}";

    public void Update(Participation participation)
    {
        var existing = Ranking.Entries.FirstOrDefault(x => x.Participation == participation);
        if (existing == null)
        {
            return;
        }
        existing.Participation = participation;
        Entries = Rank(Ranking);
    }


    static List<RankingEntry> Rank(Ranking ranking)
    {
        var ranker = StaticOption.ShouldUseRegionalRanker(ranking.Ruleset)
            ? GetRanker(StaticOption.Regional)
            : _feiRanker;
        var ranked = ranker.Rank(ranking);
        var rank = 0;
        foreach (var entry in ranked)
        {
            entry.Rank = ++rank;
        }
        return ranked;
    }

    static Ranker GetRanker(IRegionOption? configuration)
    {
        return _regionalRankers.FirstOrDefault(x => x.CountryIsoCode == configuration?.CountryIsoCode) ?? _feiRanker;
    }
}
