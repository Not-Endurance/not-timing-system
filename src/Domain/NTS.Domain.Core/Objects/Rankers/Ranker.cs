using NTS.Domain.Core.Aggregates;

namespace NTS.Domain.Core.Objects.Rankers;

internal abstract class Ranker
{
    public abstract List<RankingEntry> Rank(Ranking ranking);

    public string? CountryIsoCode { get; protected init; }
}
