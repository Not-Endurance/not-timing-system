namespace NTS.Domain.Core.Aggregates;

public class RankingEntry : Aggregate // TODO: refacator in a proper aggregate
{
    public RankingEntry(Participation? participation, int? rank, bool isNotRanked, int? id = null)
        : base(id)
    {
        Participation = Required(nameof(Participation), participation);
        Rank = rank;
        IsNotRanked = isNotRanked;
    }

    public Participation Participation { get; internal set; }
    public int? Rank { get; internal set; }
    public bool IsNotRanked { get; }

    public override string ToString()
    {
        return IsNotRanked ? $"{X_string} {Participation}" : Participation.ToString();
    }
}
