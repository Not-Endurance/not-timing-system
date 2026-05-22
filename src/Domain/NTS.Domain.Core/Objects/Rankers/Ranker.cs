using NTS.Domain.Core.Aggregates;

namespace NTS.Domain.Core.Objects.Rankers;

internal abstract class Ranker
{
    public abstract List<RankingEntry> Rank(Ranking ranking);

    public string? CountryIsoCode { get; protected init; }

    protected IOrderedEnumerable<RankingEntry> OrderByNotEliminatedAndRanked(IEnumerable<RankingEntry> entries)
    {
        return entries
            .OrderBy(x => x.Participation.IsEliminated())
            .ThenBy(x => x.IsNotRanked)
            .ThenBy(x => !x.Participation.IsComplete());
    }
}
