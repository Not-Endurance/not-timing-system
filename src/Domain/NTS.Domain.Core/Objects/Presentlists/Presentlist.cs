using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;

namespace NTS.Domain.Core.Objects.Presentlists;

public record Presentlist : ValueObject
{
    readonly List<PresentlistEntry> _entries = [];

    Presentlist(List<PresentlistEntry> entries)
    {
        _entries = Normalize(entries);
    }

    public Presentlist(IEnumerable<Participation> participations)
    {
        var entries = new UniqueParticipations(participations)
            .Where(x => !x.IsEliminated())
            .SelectMany(CreateEntries);

        _entries = Normalize(entries);
    }

    public IReadOnlyList<PresentlistEntry> Entries => _entries;

    public Presentlist With(Participation participation)
    {
        var entries = _entries
            .Where(x => x.Number != participation.Combination.Number)
            .Concat(CreateEntries(participation));
        return FromEntries(entries);
    }

    public Presentlist Without(PresentlistEntryKey key)
    {
        return FromEntries(_entries.Where(x => x.Key != key));
    }

    public Presentlist WithoutParticipation(int number)
    {
        return FromEntries(_entries.Where(x => x.Number != number));
    }

    static IEnumerable<PresentlistEntry> CreateEntries(Participation participation)
    {
        foreach (var phase in participation.Phases)
        {
            var presentation = CreatePresentationEntry(participation, phase);
            if (presentation != null)
            {
                yield return presentation;
            }

            var representation = CreateRepresentationEntry(participation, phase);
            if (representation != null)
            {
                yield return representation;
            }

            var inspection = CreateInspectionEntry(participation, phase);
            if (inspection != null)
            {
                yield return inspection;
            }
        }
    }

    static PresentlistEntry? CreatePresentationEntry(Participation participation, Phase phase)
    {
        if (phase.ArriveTime == null || phase.PresentTime != null)
        {
            return null;
        }

        return CreateEntry(participation, phase, CreateRecoveryDeadline(phase), PresentlistEntryType.Present);
    }

    static PresentlistEntry? CreateRepresentationEntry(Participation participation, Phase phase)
    {
        if (!phase.IsReinspectionRequested || phase.RepresentTime != null || phase.ArriveTime == null)
        {
            return null;
        }

        return CreateEntry(participation, phase, CreateRecoveryDeadline(phase), PresentlistEntryType.Represent);
    }

    static PresentlistEntry? CreateInspectionEntry(Participation participation, Phase phase)
    {
        if (!phase.IsRequiredInspectionRequested && !phase.IsRequiredInspectionCompulsory)
        {
            return null;
        }

        var type = phase.IsRequiredInspectionCompulsory ? PresentlistEntryType.CRI : PresentlistEntryType.RI;
        return CreateEntry(participation, phase, phase.GetRequiredInspectionTime(), type);
    }

    static Timestamp? CreateRecoveryDeadline(Phase phase)
    {
        return phase.ArriveTime?.Add(TimeSpan.FromMinutes(phase.MaxRecovery));
    }

    static PresentlistEntry? CreateEntry(
        Participation participation,
        Phase phase,
        Timestamp? time,
        PresentlistEntryType type
    )
    {
        if (time == null)
        {
            return null;
        }

        return new PresentlistEntry(
            participation.Combination.Number,
            phase.Id,
            participation.Combination.Athlete.Name,
            participation.Combination.Athlete.NameEnglish,
            participation.Competition.Ruleset,
            time,
            type
        );
    }

    static List<PresentlistEntry> OrderEntries(IEnumerable<PresentlistEntry> entries)
    {
        return entries
            .OrderBy(x => x.Time)
            .ThenBy(x => TypePriority(x.Type))
            .ThenBy(x => x.Number)
            .ToList();
    }

    static Presentlist FromEntries(IEnumerable<PresentlistEntry> entries)
    {
        return new Presentlist(entries.ToList());
    }

    static List<PresentlistEntry> Normalize(IEnumerable<PresentlistEntry> entries)
    {
        return OrderEntries(entries)
            .GroupBy(x => x.Number)
            .Select(x => x.First())
            .ToList();
    }

    static int TypePriority(PresentlistEntryType type)
    {
        return type switch
        {
            PresentlistEntryType.Present => 0,
            PresentlistEntryType.Represent => 1,
            PresentlistEntryType.CRI => 2,
            PresentlistEntryType.RI => 3,
            _ => 4,
        };
    }
}
