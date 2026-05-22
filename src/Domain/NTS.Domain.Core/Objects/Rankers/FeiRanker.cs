using NTS.Domain.Core.Aggregates;

namespace NTS.Domain.Core.Objects.Rankers;

internal class FeiRanker : Ranker
{
    public override List<RankingEntry> Rank(Ranking ranking)
    {
        return OrderByNotEliminatedAndRanked(ranking.Entries)
            .ThenBy(x => x.Participation.Phases.LastOrDefault(x => x.ArriveTime != null)?.ArriveTime)
            .ToList();
    }
}
