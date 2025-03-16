using System.Collections;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Rankers;
using NTS.Domain.Core.StaticOptions;

namespace NTS.Domain.Core.Objects;

public class Ranklist : IReadOnlyList<RankingEntry>
{
    readonly Ranking _ranking;
    static FeiRanker _feiRanker = new();
    static Ranker[] _regionalRankers = [new BulgariaRanker()];
    List<RankingEntry> _entries;

    public Ranklist(Ranking ranking)
    {
        _entries = Rank(ranking);
        _ranking = ranking;
    }

    public RankingEntry this[int index] => _entries[index];
    public int Count => _entries.Count;
    public int RankingId => _ranking.Id;
    public string Name => _ranking.Name;
    public AthleteCategory Category => _ranking.Category;
    public CompetitionType Type => _ranking.Type;
    public CompetitionRuleset Ruleset => _ranking.Ruleset;
    public string Title => $"{Category}: {Name}";

    public IEnumerator<RankingEntry> GetEnumerator()
    {
        return _entries.GetEnumerator();
    }

    public void Update(Participation participation)
    {
        var existing = _ranking.Entries.FirstOrDefault(x => x.Participation == participation);
        if (existing == null)
        {
            return;
        }
        existing.Participation = participation;
        _entries = Rank(_ranking);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
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
