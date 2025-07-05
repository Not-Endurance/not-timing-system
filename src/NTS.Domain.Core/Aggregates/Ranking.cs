using System.Collections.ObjectModel;
using Not.Domain.Base;
using NTS.Domain.Core.Aggregates.Participations;

namespace NTS.Domain.Core.Aggregates;

public class Ranking : AggregateRoot
{
    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public Ranking(
        int id,
        string name,
        CompetitionRuleset ruleset,
        CompetitionType type,
        AthleteCategory category,
        string? competitionFeiId,
        string? feiRule,
        string? feiScheduleNumber,
        ReadOnlyCollection<RankingEntry> entries
    )
        : base(id)
    {
        Name = name;
        Ruleset = ruleset;
        Category = category;
        Type = type;
        CompetitionFeiId = competitionFeiId;
        FeiRule = feiRule;
        FeiScheduleNumber = feiScheduleNumber;
        Entries = entries;
    }

    public Ranking(
        Competition competition,
        AthleteCategory category,
        string? competitionFeiId,
        string? feiRule,
        string? feiScheduleNumber,
        IEnumerable<RankingEntry> entries
    )
        : this(
            GenerateId(),
            competition.Name,
            competition.Ruleset,
            competition.Type,
            category,
            competitionFeiId,
            feiRule,
            feiScheduleNumber,
            new(entries.ToList())
        ) { }

    public string Name { get; }
    public CompetitionRuleset Ruleset { get; }
    public CompetitionType Type { get; }
    public AthleteCategory Category { get; }
    public string? CompetitionFeiId { get; }
    public string? FeiRule { get; }
    public string? FeiScheduleNumber { get; }
    public ReadOnlyCollection<RankingEntry> Entries { get; }

    public override string ToString()
    {
        return $"{Name} {Category}: {Entries.Count}";
    }
}
