using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Documents.EnduranceEvents.Models;

public class RankingEntryModel
{
    public RankingEntryModel(RankingEntry rankingEntry)
    {
        Participation = new ParticipationModel(rankingEntry.Participation);
        Rank = rankingEntry.Rank;
        IsNotRanked = rankingEntry.IsNotRanked;
    }

    public ParticipationModel Participation { get; init; }
    public int? Rank { get; init; }
    public bool IsNotRanked { get; init; }
}
