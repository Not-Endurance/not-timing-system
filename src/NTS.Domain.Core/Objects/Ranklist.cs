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
    public ParticipationCategory Category => Ranking.Category;
    public CompetitionType Type => Ranking.Type;
    public CompetitionRuleset Ruleset => Ranking.Ruleset;
    public string Title => $"{Category}: {Name}";

    public bool IsFeiExportConfigured => !string.IsNullOrWhiteSpace(Ranking.CompetitionFeiId) &&
                                         !string.IsNullOrWhiteSpace(Ranking.FeiRule) &&
                                         !string.IsNullOrWhiteSpace(Ranking.FeiScheduleNumber);

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
