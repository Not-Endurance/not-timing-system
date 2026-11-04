using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Judge.Contracts.Features.Core.Rankings.CustomRankings;

public class CustomRankingEntryModel
{
    public Participation? Participation { get; set; }
    public bool IsNotRanked { get; set; }
}
