using NTS.Domain.Core.Aggregates.Participations;

namespace NTS.Domain.Core.Entities;

public class RankingEntry : DomainEntity
{
    private RankingEntry(int id) : base(id)
    {
    }
    public RankingEntry(Participation participation, bool isRanked)
    {
        Participation = participation;
        IsRanked = isRanked;
    }

    public Participation Participation { get; private set; }
    public bool IsRanked { get; private set; }

    public override string ToString()
    {
        return $"IsRanked: {IsRanked}, {Participation}";
    }
}
