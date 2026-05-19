using Not.Domain.Krud;
using NTS.Domain.Extensions;

namespace NTS.Domain.Setup.Aggregates.ConfigureEvents;

public class Competition : Entity, IKrudParent<Participation>, IKrudParent<Phase>
{
    readonly List<Phase> _phases = [];
    readonly List<Participation> _participations = [];

    public Competition(
        string? name,
        CompetitionType? type,
        CompetitionRuleset? ruleset,
        DateTimeOffset? start,
        TimeSpan? compulsoryThresholdSpan,
        string? feiEventId,
        string? feiEventCode,
        string? feiCompetitionId,
        string? feiRule,
        string? feiScheduleNumber,
        IEnumerable<Phase> phases,
        IEnumerable<Participation> participations,
        int? id = null
    )
        : base(id)
    {
        _phases = phases.ToList();
        _participations = participations.ToList();
        Name = Required(nameof(Name), name);
        Type = Required(nameof(Type), type);
        Ruleset = Required(nameof(Ruleset), ruleset);
        Start = Required(nameof(Start), start);
        CompulsoryThresholdSpan = compulsoryThresholdSpan;
        FeiEventId = feiEventId;
        FeiEventCode = feiEventCode;
        FeiCompetitionId = feiCompetitionId;
        FeiRule = feiRule;
        FeiScheduleNumber = feiScheduleNumber;

        foreach (var participation in _participations)
        {
            participation.SetSpeedLimits(Type);
        }
    }

    IReadOnlyList<Participation> IKrudParent<Participation>.Children => Participations;
    IReadOnlyList<Phase> IKrudParent<Phase>.Children => Phases;

    public string Name { get; }
    public CompetitionType Type { get; }
    public CompetitionRuleset Ruleset { get; }
    public DateTimeOffset Start { get; }
    public TimeSpan? CompulsoryThresholdSpan { get; }
    public string? FeiEventId { get; }
    public string? FeiEventCode { get; }
    public string? FeiCompetitionId { get; }
    public string? FeiRule { get; } = "E Comp";
    public string? FeiScheduleNumber { get; }
    public IReadOnlyList<Phase> Phases => _phases.AsReadOnly();
    public IReadOnlyList<Participation> Participations => _participations.AsReadOnly();

    public override string ToString()
    {
        var type = Localize(Type);
        return Combine($"{Name} ({Phases.Count})", type, $"{Start.LocalDateTime:g}");
    }

    public void Add(Participation child)
    {
        child.SetSpeedLimits(Type);
        _participations.Add(child);
    }

    public void Remove(Participation child)
    {
        _participations.Remove(child);
    }

    public void Update(Participation child)
    {
        child.SetSpeedLimits(Type);
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
