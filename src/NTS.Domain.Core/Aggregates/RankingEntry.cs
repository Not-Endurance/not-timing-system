using Not.Domain.Aggregates;

namespace NTS.Domain.Core.Aggregates;

public class RankingEntry : AggregateRoot
{
    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public RankingEntry(int id, Participation? participation, int? rank, bool isNotRanked)
        : base(id)
    {
        Participation = Required(nameof(Participation), participation);
        Rank = rank;
        IsNotRanked = isNotRanked;
    }

    public RankingEntry(Participation? participation, bool isNotRanked)
        : this(GenerateId(), participation, null, isNotRanked) { }

    public Participation Participation { get; internal set; }
    public int? Rank { get; internal set; }
    public bool IsNotRanked { get; }

    public override string ToString()
    {
        return IsNotRanked ? $"{X_string} {Participation}" : Participation.ToString();
    }
}
