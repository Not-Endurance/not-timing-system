using NoTiming.Domain.Core.Aggregates.Results;

namespace NoTiming.Domain.Core.Objects.Rankers;

internal class FeiRanker : Ranker
{
    public override List<ParticipationResult> Rank(IEnumerable<ParticipationResult> entries)
    {
        return OrderByNotEliminatedAndRanked(entries)
            .ThenBy(x => x.Participation.Phases.LastOrDefault(x => x.ArriveTime != null)?.ArriveTime)
            .ToList();
    }
}
