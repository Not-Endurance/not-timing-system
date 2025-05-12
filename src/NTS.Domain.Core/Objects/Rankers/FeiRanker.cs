using NTS.Domain.Core.Aggregates;

namespace NTS.Domain.Core.Objects.Rankers;

internal class FeiRanker : Ranker
{
    protected IOrderedEnumerable<RankingEntry> OrderByNotEliminatedAndRanked(IEnumerable<RankingEntry> entries)
    {
        return entries
            .OrderBy(x => x.Participation.IsEliminated())
            .ThenBy(x => x.IsNotRanked)
            .ThenBy(x => !x.Participation.IsComplete());
    }

    public override List<RankingEntry> Rank(Ranking ranking)
    {
        return OrderByNotEliminatedAndRanked(ranking.Entries)
            .ThenBy(x => x.Participation.Phases.LastOrDefault(x => x.ArriveTime != null)?.ArriveTime)
            .ToList();
    }
}
