using NTS.Domain.Core.Aggregates;

namespace NTS.Domain.Core.Objects.Rankers;

internal class BulgariaRanker : FeiRanker
{
    public BulgariaRanker()
    {
        CountryIsoCode = "BGR";
    }

    public override List<RankingEntry> Rank(Ranking ranking)
    {
        if (ranking.Category == ParticipationCategory.Children && ranking.Type != CompetitionType.Championship)
        {
            return OrderByNotEliminatedAndRanked(ranking.Entries)
                .ThenBy(x => x.Participation.GetTotal()?.RecoveryIntervalWithoutFinal)
                .ToList();
        }
        return base.Rank(ranking);
    }
}
