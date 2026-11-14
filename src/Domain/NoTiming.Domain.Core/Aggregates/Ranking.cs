using System.Collections.ObjectModel;
using NC.Domain.Exceptions;

namespace NoTiming.Domain.Core.Aggregates;

public class Ranking : Aggregate, IEventScoped
{
    public Ranking(
        string? name,
        bool englishNames,
        ParticipationCategory? category,
        string? feiEventId,
        string? feiEventCode,
        string? feiCompetitionId,
        string? feiRule,
        string? feiScheduleNumber,
        IEnumerable<RankingEntry> entries,
        int eventId,
        int? id = null,
        int? storedDomainVersion = null
    )
        : base(id, storedDomainVersion: storedDomainVersion)
    {
        EventId = eventId;
        Name = Required(nameof(Name), name);
        EnglishNames = englishNames;
        Category = Required(nameof(Category), category);
        var rankingEntries = AreUnique(nameof(Entries), entries).ToList();
        EnsurePlacingsAreAllOrNone(rankingEntries);
        Entries = new(rankingEntries);
        FeiEventId = feiEventId;
        FeiEventCode = feiEventCode;
        FeiCompetitionId = feiCompetitionId;
        FeiRule = feiRule;
        FeiScheduleNumber = feiScheduleNumber;
    }

    public int EventId { get; }
    public string Name { get; }
    public bool EnglishNames { get; }
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

    public bool Update(Participation participation)
    {
        var existing = Entries.FirstOrDefault(x => x.Participation.Id == participation.Id);
        if (existing == null)
        {
            return false;
        }
        existing.Participation = participation;
        return true;
    }

    // Stored placings are final. A ranking that has some placings must have one for every entry, so a
    // participation added without a placing can't silently re-rank the stored ones.
    static void EnsurePlacingsAreAllOrNone(IReadOnlyCollection<RankingEntry> entries)
    {
        var rankedCount = entries.Count(x => x.Rank != null);
        if (rankedCount != 0 && rankedCount != entries.Count)
        {
            throw new DomainException(
                "Placings are stored for some entries but not all. Stored placings are final, so a participation without one can't be added."
            );
        }
    }
}
