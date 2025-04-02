using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Not.Domain.Base;
using NTS.Domain.Core.Aggregates.Participations;

namespace NTS.Domain.Core.Aggregates;

public class Ranking : AggregateRoot
{
    [JsonConstructor]
    Ranking(
        int id,
        string name,
        CompetitionRuleset ruleset,
        CompetitionType type,
        AthleteCategory category,
        string? feiRule,
        string? feiEventCode,
        string? feiScheduleNumber,
        string? feiCategoryEventNumber,
        ReadOnlyCollection<RankingEntry> entries
    )
        : base(id)
    {
        Name = name;
        Ruleset = ruleset;
        Category = category;
        Type = type;
        FeiRule = feiRule;
        FeiEventCode = feiEventCode;
        FeiScheduleNumber = feiScheduleNumber;
        FeiCategoryEventNumber = feiCategoryEventNumber;
        Entries = entries;
    }

    public Ranking(Competition competition, AthleteCategory category, string? feiRule, string? feiEventCode, string? feiScheduleNumber, string? feiCategoryEventNumber, IEnumerable<RankingEntry> entries)
        : this(GenerateId(), competition.Name, competition.Ruleset, competition.Type, category, feiRule, feiEventCode, feiScheduleNumber, feiCategoryEventNumber, new(entries.ToList()))
    { }

    public string Name { get; }
    public CompetitionRuleset Ruleset { get; }
    public CompetitionType Type { get; }
    public AthleteCategory Category { get; }
    public string? FeiRule { get; }
    public string? FeiEventCode { get; }
    public string? FeiScheduleNumber { get; }
    public string? FeiCategoryEventNumber { get; }
    public ReadOnlyCollection<RankingEntry> Entries { get; }

    public override string ToString()
    {
        return $"{Name} {Category}: {Entries.Count}";
    }
}
