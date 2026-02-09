using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Blazor.Features.Core.Rankings.CustomRanking;

public class RankingEntryModel
{
    public Participation? Participation { get; set; }
    public bool IsNotRanked { get; set; }
}
