using NoTiming.Domain.Core.Aggregates.Results;

namespace NoTiming.Domain.Core.Objects.Rankers;

internal class FeiRanker : Ranker
{
    public override List<ParticipationResult> Rank(IEnumerable<ParticipationResult> entries)
    {
        return OrderByNotEliminatedAndRanked(entries)
            .ThenBy(x => LastArriveTime(x) == null)
            .ThenBy(x => LastArriveTime(x))
            .ToList();
    }

    static DateTimeOffset? LastArriveTime(ParticipationResult entry)
    {
        return entry.Participation.Phases.LastOrDefault(x => x.ArriveTime != null)?.ArriveTime;
    }
}
