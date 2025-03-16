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
        if (ranking.Category == AthleteCategory.Children && ranking.Type != CompetitionType.Championship)
        {
            return OrderByNotEliminatedAndRanked(ranking.Entries)
                .ThenBy(x => x.Participation.GetTotal()?.RecoveryIntervalWithoutFinal)
                .ToList();
        }
        else
        {
            return base.Rank(ranking);
        }
    }
}
