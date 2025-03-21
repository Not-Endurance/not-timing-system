using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Not.Domain.Base;
using NTS.Domain.Core.Aggregates.Participations;

namespace NTS.Domain.Core.Aggregates.Temp;

public class Ranking2 : AggregateRoot, IAggregateRoot
{
    [JsonConstructor]
    public Ranking2(
        int id,
        string name,
        CompetitionRuleset ruleset,
        CompetitionType type,
        AthleteCategory category,
        ReadOnlyCollection<RankingEntry2> entries
    )
        : base(id)
    {
        Name = name;
        Ruleset = ruleset;
        Category = category;
        Type = type;
        Entries = entries;
    }

    public Ranking2(Competition competition, AthleteCategory category, IEnumerable<RankingEntry2> entries)
        : this(GenerateId(), competition.Name, competition.Ruleset, competition.Type, category, new(entries.ToList()))
    { }

    public string Name { get; }
    public CompetitionRuleset Ruleset { get; }
    public CompetitionType Type { get; }
    public AthleteCategory Category { get; }
    public ReadOnlyCollection<RankingEntry2> Entries { get; }

    public override string ToString()
    {
        return $"{Name} {Category}: {Entries.Count}";
    }
}
