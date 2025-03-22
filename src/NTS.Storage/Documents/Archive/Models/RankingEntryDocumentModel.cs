using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Documents.Archive.Models;

public class RankingEntryDocumentModel
{
    public RankingEntryDocumentModel(RankingEntry rankingEntry)
    {
        Participation = new ParticipationDocumentModel(rankingEntry.Participation);
        Rank = rankingEntry.Rank;
        IsNotRanked = rankingEntry.IsNotRanked;
    }

    public ParticipationDocumentModel Participation { get; init; }
    public int? Rank { get; init; }
    public bool IsNotRanked { get; init; }
}
