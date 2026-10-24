using System.Collections.ObjectModel;
using Not.Domain.Exceptions;

namespace NTS.Domain.Core.Aggregates;

public class Ranking : Aggregate, IEventScoped
{
    public Ranking(
        string? name,
        CompetitionRuleset? ruleset,
        ParticipationCategory? category,
        string? feiEventId,
        string? feiEventCode,
        string? feiCompetitionId,
        string? feiRule,
        string? feiScheduleNumber,
        IEnumerable<RankingEntry> entries,
        Guid eventId,
        Guid? id = null
    )
        : base(id)
    {
        EventId = eventId;
        Name = Required(nameof(Name), name);
        Ruleset = Required(nameof(Ruleset), ruleset);
        Category = Required(nameof(Category), category);
        Entries = OnePerParticipation(entries);
        FeiEventId = feiEventId;
        FeiEventCode = feiEventCode;
        FeiCompetitionId = feiCompetitionId;
        FeiRule = feiRule;
        FeiScheduleNumber = feiScheduleNumber;
    }

    public Guid EventId { get; }
    public string Name { get; }
    public CompetitionRuleset Ruleset { get; }
    public ParticipationCategory Category { get; }
    public string? FeiEventId { get; }
    public string? FeiEventCode { get; }
    public string? FeiCompetitionId { get; }
    public string? FeiRule { get; }
    public string? FeiScheduleNumber { get; }
    public ReadOnlyCollection<RankingEntry> Entries { get; }

    public override string ToString()
    {
        return $"{Name} {Category}: {Entries.Count}";
    }

    static ReadOnlyCollection<RankingEntry> OnePerParticipation(IEnumerable<RankingEntry> entries)
    {
        var counted = entries.ToList();
        if (counted.GroupBy(x => x.ParticipationId).Any(x => x.Count() > 1))
        {
            throw new DomainPropertyException(nameof(Entries), "Collection_contains_duplicate_entries");
        }

        return counted.AsReadOnly();
    }
}
