using NTS.Domain.Core.Aggregates;

namespace NTS.Application.Contracts.Core.Models;

public class RankingEntryModel
{
    public static RankingEntryModel MapFrom(RankingEntry rankingEntry)
    {
        return new RankingEntryModel
        {
            Participation = ParticipationModel.MapFrom(rankingEntry.Participation),
            Rank = rankingEntry.Rank,
            IsNotRanked = rankingEntry.IsNotRanked,
        };
    }

    public ParticipationModel Participation { get; init; } = default!;
    public int? Rank { get; init; }
    public bool IsNotRanked { get; init; }

    public RankingEntry MapToEntity()
    {
        var participation = Participation.MapToEntity();
        return new RankingEntry(participation, Rank, IsNotRanked);
    }
}
