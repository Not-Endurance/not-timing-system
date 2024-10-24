using Newtonsoft.Json;
using Not.Localization;

namespace NTS.Domain.Core.Entities;

public class RankingEntry : DomainEntity
{
    [JsonConstructor]
    private RankingEntry(int id, Participation participation, bool isNotRanked) : base(id)
    {
        Participation = participation;
        IsNotRanked = isNotRanked;
    }
    public RankingEntry(Participation participation, bool isNotRanked) : this(GenerateId(), participation, isNotRanked)
    {
    }

    public Participation Participation { get; internal set; }
    public bool IsNotRanked { get; } 

    public override string ToString()
    {
        var message = IsNotRanked
            ? $"({"not ranked".Localize()}) "
            : "";
        return message + Participation.ToString();
    }
}
