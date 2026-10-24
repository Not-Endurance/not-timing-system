using Not.Domain.Exceptions;

namespace NTS.Domain.Core.Aggregates;

/// <summary>
/// A line of a Ranking: the Participation it counts and whether this Ranking leaves it out of the placings. A
/// Participation can be in several Rankings and has a mark of its own in each. The rank is the final placing (ADR-0006):
/// null while the Event is Live, when the Results compute the ranks.
/// </summary>
public sealed record RankingEntry
{
    public RankingEntry(Guid participationId, bool isNotRanked, int? rank = null)
    {
        if (participationId == Guid.Empty)
        {
            throw new DomainPropertyException(
                nameof(ParticipationId),
                string.Format(Field_1_is_required_on_2_string, nameof(ParticipationId), nameof(RankingEntry))
            );
        }

        ParticipationId = participationId;
        IsNotRanked = isNotRanked;
        Rank = rank;
    }

    public Guid ParticipationId { get; }
    public bool IsNotRanked { get; }
    public int? Rank { get; }
}
