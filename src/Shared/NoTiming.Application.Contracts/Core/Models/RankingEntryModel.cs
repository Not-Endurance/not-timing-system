using NoTiming.Application.Contracts.Shared;
using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Application.Contracts.Core.Models;

public class RankingEntryModel : PersistedModel
{
    public static RankingEntryModel MapFrom(RankingEntry rankingEntry)
    {
        return new RankingEntryModel
        {
            DomainVersion = rankingEntry.DomainVersion,
            Participation = ParticipationModel.MapFrom(rankingEntry.Participation),
            Rank = rankingEntry.Rank,
            IsNotRanked = rankingEntry.IsNotRanked,
        };
    }

    public ParticipationModel Participation { get; init; } = default!;
    public int? Rank { get; set; }
    public bool IsNotRanked { get; init; }

    public RankingEntry MapToEntity()
    {
        var participation = Participation.MapToEntity();
        return new RankingEntry(participation, Rank, IsNotRanked, storedDomainVersion: DomainVersion);
    }
}
