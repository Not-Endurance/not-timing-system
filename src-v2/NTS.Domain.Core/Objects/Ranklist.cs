using NTS.Domain.Core.Configuration;
using NTS.Domain.Core.Entities;
using NTS.Domain.Core.Objects.Regional;
using System.Collections.ObjectModel;

namespace NTS.Domain.Core.Objects;

public class Ranklist : ReadOnlyCollection<RankingEntry>
{
    private readonly static FeiRanker _feiRanker = new();
    private readonly static Ranker[] _regionalRankers = [ new BulgariaRanker() ];

    public Ranklist(Ranking ranking) : base(Rank(ranking))
    {
        RankingId = ranking.Id;
        Name = ranking.Name;
        Category = ranking.Category;
        Ruleset = ranking.Ruleset;
    }

    public int RankingId { get; }
    public string Title => $"{Category}: {Name}";
    public string Name { get; }
    public AthleteCategory Category { get; }
    public CompetitionRuleset Ruleset { get; }

    private static IList<RankingEntry> Rank(Ranking ranking)
    {
        var ranker = StaticOptions.ShouldUseRegionalRanker(ranking.Ruleset)
            ? GetRanker(StaticOptions.RegionalConfiguration)
            : _feiRanker;
        return ranker.Rank(ranking);
    }

    private static Ranker GetRanker(IRegionalConfiguration? configuration)
    {
        return _regionalRankers.FirstOrDefault(x => x.CountryIsoCode == configuration?.CountryIsoCode) ?? _feiRanker;
    }
}   
