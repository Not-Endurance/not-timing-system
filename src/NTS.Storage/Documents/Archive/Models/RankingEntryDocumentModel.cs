using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Documents.Archive.Models;

public class RankingEntryDocumentModel
{
    public static RankingEntryDocumentModel Create(RankingEntry rankingEntry)
    {
        return new RankingEntryDocumentModel
        {
            Participation = ParticipationDocumentModel.Create(rankingEntry.Participation),
            Rank = rankingEntry.Rank,
            IsNotRanked = rankingEntry.IsNotRanked,
        };
    }

    public ParticipationDocumentModel Participation { get; init; } = default!;
    public int? Rank { get; init; }
    public bool IsNotRanked { get; init; }
}
