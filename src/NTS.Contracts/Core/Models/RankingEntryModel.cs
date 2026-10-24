using NTS.Domain.Core.Aggregates;

namespace NTS.Contracts.Core.Models;

/// <summary>A line of a stored Ranking: the id of the Participation it counts, never the Participation (ADR-0006).</summary>
public class RankingEntryModel
{
    public static RankingEntryModel MapFrom(RankingEntry rankingEntry)
    {
        return new RankingEntryModel
        {
            ParticipationId = rankingEntry.ParticipationId,
            IsNotRanked = rankingEntry.IsNotRanked,
            Rank = rankingEntry.Rank,
        };
    }

    public Guid ParticipationId { get; init; }
    public bool IsNotRanked { get; init; }
    public int? Rank { get; init; }

    public RankingEntry MapToEntity()
    {
        return new RankingEntry(ParticipationId, IsNotRanked, Rank);
    }
}
