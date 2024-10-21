using NTS.Domain.Core.Entities;

namespace NTS.Domain.Core.Objects.Regional;

internal class FeiRanker : Ranker
{
    public override IList<RankingEntry> Rank(Ranking ranking)
    {
        return OrderByNotEliminatedAndRanked(ranking.Entries)
            .ThenBy(x => x.Participation.Phases.Last().ArriveTime)
            .ToList();
    }

    protected IOrderedEnumerable<RankingEntry> OrderByNotEliminatedAndRanked(IEnumerable<RankingEntry> entries)
    {
        return entries
            .OrderBy(x => x.Participation.IsEliminated())
            .ThenBy(x => x.IsNotRanked);
    }
}

internal abstract class Ranker
{
    public string? CountryIsoCode { get; protected set; }
    public abstract IList<RankingEntry> Rank(Ranking ranking);
}
