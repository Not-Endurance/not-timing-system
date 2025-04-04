using Not.Domain.Base;

namespace NTS.Domain.Core.Aggregates;

public class RankingEntry : AggregateRoot, IAggregateRoot
{
    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public RankingEntry(int id, Participation participation, int? rank, bool isNotRanked)
        : base(id)
    {
        Participation = participation;
        Rank = rank;
        IsNotRanked = isNotRanked;
    }

    public RankingEntry(Participation participation, bool isNotRanked)
        : this(GenerateId(), participation, null, isNotRanked) { }

    public Participation Participation { get; internal set; }
    public int? Rank { get; internal set; }
    public bool IsNotRanked { get; }

    public override string ToString()
    {
        var message = IsNotRanked ? $"({not_ranked_string}) " : "";
        return message + Participation.ToString();
    }
}
