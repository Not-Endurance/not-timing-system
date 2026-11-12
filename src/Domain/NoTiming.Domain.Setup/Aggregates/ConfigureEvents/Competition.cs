using NC.Domain.Krud;
using NoTiming.Domain.Extensions;
using NoTiming.Domain.Objects;

namespace NoTiming.Domain.Setup.Aggregates.ConfigureEvents;

public class Competition : Entity, IKrudParent<Participation>, IKrudParent<Phase>
{
    readonly List<Phase> _phases = [];
    readonly List<Participation> _participations = [];

    public Competition(
        string? name,
        bool englishNames,
        DateTimeOffset? start,
        TimeSpan? compulsoryThresholdSpan,
        SpeedRestrictions? speedRestrictions,
        string? feiEventId,
        string? feiEventCode,
        string? feiCompetitionId,
        string? feiRule,
        string? feiScheduleNumber,
        IEnumerable<Phase> phases,
        IEnumerable<Participation> participations,
        int? id = null,
        int? storedDomainVersion = null
    )
        : base(id, storedDomainVersion: storedDomainVersion)
    {
        _phases = phases.ToList();
        _participations = participations.ToList();
        Name = Required(nameof(Name), name);
        EnglishNames = englishNames;
        Start = Required(nameof(Start), start);
        CompulsoryThresholdSpan = compulsoryThresholdSpan;
        Restrictions = speedRestrictions ?? SpeedRestrictions.None;
        FeiEventId = feiEventId;
        FeiEventCode = feiEventCode;
        FeiCompetitionId = feiCompetitionId;
        FeiRule = feiRule;
        FeiScheduleNumber = feiScheduleNumber;
    }

    IReadOnlyList<Participation> IKrudParent<Participation>.Children => Participations;
    IReadOnlyList<Phase> IKrudParent<Phase>.Children => Phases;

    public string Name { get; }
    public bool EnglishNames { get; }
    public DateTimeOffset Start { get; }
    public TimeSpan? CompulsoryThresholdSpan { get; }
    public SpeedRestrictions Restrictions { get; }
    public string? FeiEventId { get; }
    public string? FeiEventCode { get; }
    public string? FeiCompetitionId { get; }
    public string? FeiRule { get; } = "E Comp";
    public string? FeiScheduleNumber { get; }
    public IReadOnlyList<Phase> Phases => _phases.AsReadOnly();
    public IReadOnlyList<Participation> Participations => _participations.AsReadOnly();

    public override string ToString()
    {
        return Combine($"{Name} ({Phases.Count})", $"{Start.LocalDateTime:g}");
    }

    public void Add(Participation child)
    {
        _participations.Add(child);
    }

    public void Remove(Participation child)
    {
        _participations.Remove(child);
    }

    public void Update(Participation child)
    {
        _participations.Update(child);
    }

    public void Add(Phase child)
    {
        _phases.Add(child);
    }

    public void Remove(Phase child)
    {
        _phases.Remove(child);
    }

    public void Update(Phase child)
    {
        _phases.Update(child);
    }
}
