using NTS.Domain.Core.Entities;

namespace NTS.Domain.Core.Objects.Regional;

internal class BulgariaRanker : FeiRanker
{
    public BulgariaRanker()
    {
        CountryIsoCode = "BGR";
    }

    public override List<RankingEntry> Rank(Ranking ranking)
    {
        if (ranking.Category != AthleteCategory.Senior)
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