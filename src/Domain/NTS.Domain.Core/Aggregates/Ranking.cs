using System.Collections.ObjectModel;
using Not.Domain.Exceptions;
using NTS.Domain.Access;

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

    /// <summary>
    /// Whether every entry holds its final placing (ADR-0006): null while the Event is Live, when the Results rank in memory,
    /// and stored once it has ended. The Results of a final Ranking read the stored ranks.
    /// </summary>
    public bool IsFinal => Entries.All(x => x.Rank != null);

    /// <summary>
    /// Stores the placing of every entry, composed by the code that composes the Results over the Participations of the
    /// Event and the rules of the Event, once the Event has ended, and never before (the Event is Historic, ADR-0007): the
    /// finalisation is derived from data that can no longer change, so it can be made again. A Ranking with a single entry
    /// is finalised like any other, its only entry being rank 1. A Ranking that is final is left as it is, and so is one
    /// with only some ranks stored, which nothing here produces and which is reported. A Ranking is not itself changed:
    /// the one to keep comes back.
    /// </summary>
    public RankingFinalisation Finalise(
        IEnumerable<Participation> participations,
        RegionalRules? rules,
        EventStage stage
    )
    {
        if (stage != EventStage.Historic)
        {
            throw GuardHelper.Exception(
                $"Ranking {Id} cannot be finalised while its Event is {stage}: its placings are final once the Event has ended."
            );
        }

        if (IsFinal)
        {
            return new RankingFinalisation(this, RankingFinalisationOutcome.AlreadyFinal);
        }

        if (Entries.Any(x => x.Rank != null))
        {
            return new RankingFinalisation(this, RankingFinalisationOutcome.SomePlacingsStored);
        }

        var placings = Result.PlacingsOf(this, participations, rules);
        var placed = new Ranking(
            Name,
            Ruleset,
            Category,
            FeiEventId,
            FeiEventCode,
            FeiCompetitionId,
            FeiRule,
            FeiScheduleNumber,
            Entries.Select(x => new RankingEntry(x.ParticipationId, x.IsNotRanked, placings[x.ParticipationId])),
            EventId,
            Id
        );
        return new RankingFinalisation(placed, RankingFinalisationOutcome.Finalised);
    }

    /// <summary>
    /// Whether this is the Ranking of the ride's own Competition and Category (#638, ADR-0006): the one that the start of the
    /// Event makes for each Category of each Competition, which carries the name and the ruleset of the Competition and the
    /// Category of the ride. A Ranking that the Main Operator made and that happens to share all three is not told from it.
    /// </summary>
    public bool IsOwnOf(Participation participation)
    {
        return Name == participation.Competition.Name
            && Ruleset == participation.Competition.Ruleset
            && Category == participation.Category;
    }

    /// <summary>
    /// What the ride placed as in this Ranking (#638): the elimination as the Results show it, or not ranked, or the rank of its
    /// entry. A final Ranking gives the rank it stores, whatever the times would rank the ride now, and needs of the Event's
    /// Participations only the ride itself; one that is not final ranks the ride in memory as the Results do, over all the
    /// Participations it counts, and its only entry is rank 1, which the Results leave unranked. A Ranking that counts a
    /// Participation that is not given cannot be ranked in memory, and that is refused as it is for the Results.
    /// </summary>
    /// <param name="participation">The ride, which this Ranking has to count.</param>
    /// <param name="participations">The Participations of the Event, read only while the Ranking is not final.</param>
    public Placing PlacingOf(
        Participation participation,
        IEnumerable<Participation> participations,
        RegionalRules? rules
    )
    {
        var entry =
            Entries.FirstOrDefault(x => x.ParticipationId == participation.Id)
            ?? throw GuardHelper.Exception($"Ranking {Id} does not count Participation {participation.Id}.");
        if (participation.Eliminated is { } eliminated)
        {
            return Placing.Eliminated(eliminated.ToString());
        }

        if (entry.IsNotRanked)
        {
            return Placing.NotRanked();
        }

        return Placing.Ranked(IsFinal ? entry.Rank!.Value : RankInMemory(participation, participations, rules));
    }

    public override string ToString()
    {
        return $"{Name} {Category}: {Entries.Count}";
    }

    int RankInMemory(Participation participation, IEnumerable<Participation> participations, RegionalRules? rules)
    {
        var placed = new Result(this, participations, rules).Entries.First(x => x.ParticipationId == participation.Id);
        return placed.Rank ?? 1;
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
