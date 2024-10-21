using Newtonsoft.Json;
using NTS.Domain.Core.Entities.ParticipationAggregate;
using System.Collections.ObjectModel;

namespace NTS.Domain.Core.Entities;

public class Ranking : DomainEntity, IAggregateRoot
{
    [JsonConstructor]
    private Ranking(int id, string name, CompetitionRuleset ruleset, AthleteCategory category, ReadOnlyCollection<RankingEntry> entries)
        : base(id)
    {
        Name = name;
        Ruleset = ruleset;
        Category = category;
        Entries = entries;
    }
    public Ranking(Competition competition, AthleteCategory category, IEnumerable<RankingEntry> entries)
        : this(
            GenerateId(),
            competition.Name,
            competition.Ruleset,
            category,
            new(entries.ToList()))
    {
    }

    public string Name { get; private set; }
    public CompetitionRuleset Ruleset { get; private set; }
    public AthleteCategory Category { get;private set; }
    public ReadOnlyCollection<RankingEntry> Entries { get; private set; }

    public override string ToString()
    {
        return $"{Name} {Category}: {Entries.Count}";
    }
}
