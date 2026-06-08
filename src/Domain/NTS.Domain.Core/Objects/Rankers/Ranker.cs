using NTS.Domain.Core.Aggregates.Results;

namespace NTS.Domain.Core.Objects.Rankers;

internal abstract class Ranker
{
    public abstract List<ParticipationResult> Rank(IEnumerable<ParticipationResult> entries);

    public string? CountryIsoCode { get; protected init; }

    protected IOrderedEnumerable<ParticipationResult> OrderByNotEliminatedAndRanked(
        IEnumerable<ParticipationResult> entries
    )
    {
        return entries
            .OrderBy(x => x.Participation.IsEliminated())
            .ThenBy(x => x.IsNotRanked)
            .ThenBy(x => !x.Participation.IsComplete());
    }
}
