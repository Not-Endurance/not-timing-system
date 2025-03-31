using Newtonsoft.Json;
using Not.Domain.Base;
using Not.Localization;

namespace NTS.Domain.Core.Aggregates;

public class RankingEntry : AggregateRoot, IAggregateRoot
{
    [JsonConstructor]
    RankingEntry(int id, Participation participation, bool isNotRanked)
        : base(id)
    {
        Participation = participation;
        IsNotRanked = isNotRanked;
    }

    public RankingEntry(Participation participation, bool isNotRanked)
        : this(GenerateId(), participation, isNotRanked) { }

    public Participation Participation { get; internal set; }
    public int? Rank { get; internal set; }
    public bool IsNotRanked { get; }

    public override string ToString()
    {
        var message = IsNotRanked ? $"({not_ranked_string}) " : "";
        return message + Participation.ToString();
    }
}
