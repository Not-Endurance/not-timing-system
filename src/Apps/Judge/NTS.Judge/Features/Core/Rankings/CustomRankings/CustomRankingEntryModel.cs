using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Features.Core.Rankings.CustomRankings;

public class CustomRankingEntryModel
{
    public Participation? Participation { get; set; }
    public bool IsNotRanked { get; set; }
}
