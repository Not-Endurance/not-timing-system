using Newtonsoft.Json;
using Not.Domain.Base;
using Not.Localization;

namespace NTS.Domain.Core.Aggregates.Temp;

public class RankingEntry2 : AggregateRoot, IAggregateRoot
{
    [JsonConstructor]
    public RankingEntry2(int id, Participation2 participation, bool isNotRanked)
        : base(id)
    {
        Participation = participation;
        IsNotRanked = isNotRanked;
    }

    public RankingEntry2(Participation2 participation, bool isNotRanked)
        : this(GenerateId(), participation, isNotRanked) { }

    public Participation2 Participation { get; internal set; }
    public int? Rank { get; internal set; }
    public bool IsNotRanked { get; }

    public override string ToString()
    {
        var message = IsNotRanked ? $"({"not ranked".Localize()}) " : "";
        return message + Participation.ToString();
    }
}
