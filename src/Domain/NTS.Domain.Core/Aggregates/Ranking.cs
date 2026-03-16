using System.Collections.ObjectModel;

namespace NTS.Domain.Core.Aggregates;

public class Ranking : Aggregate
{
    public Ranking(
        string? name,
        CompetitionRuleset? ruleset,
        CompetitionType? type,
        ParticipationCategory? category,
        string? competitionFeiId,
        string? feiRule,
        string? feiScheduleNumber,
        IEnumerable<RankingEntry> entries,
        int eventId,
        int? id = null
    )
        : base(id)
    {
        EventId = eventId;
        Name = Required(nameof(Name), name);
        Ruleset = Required(nameof(Ruleset), ruleset);
        Category = Required(nameof(Category), category);
        Type = Required(nameof(Type), type);
        Entries = new(AreUnique(nameof(Entries), entries).ToList());
        CompetitionFeiId = competitionFeiId;
        FeiRule = feiRule;
        FeiScheduleNumber = feiScheduleNumber;
    }

    public int EventId { get; }
    public string Name { get; }
    public CompetitionRuleset Ruleset { get; }
    public CompetitionType Type { get; }
    public ParticipationCategory Category { get; }
    public string? CompetitionFeiId { get; }
    public string? FeiRule { get; }
    public string? FeiScheduleNumber { get; }
    public ReadOnlyCollection<RankingEntry> Entries { get; }

    public override string ToString()
    {
        return $"{Name} {Category}: {Entries.Count}";
    }

    public void Update(Participation participation)
    {
        var existing = Entries.FirstOrDefault(x => x.Participation == participation);
        if (existing == null)
        {
            return;
        }
        existing.Participation = participation;
    }
}
