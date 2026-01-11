using Newtonsoft.Json;
using Not.Domain.Aggregates;
using Not.Domain.Exceptions;
using NTS.Domain.Extensions;

namespace NTS.Domain.Setup.Aggregates;

public class Competition : AggregateRoot, IParent<Participation>, IParent<Phase>
{
    readonly List<Phase> _phases = [];
    readonly List<Participation> _participations = [];

    public Competition(
        string? name,
        CompetitionType? type,
        CompetitionRuleset ruleset,
        DateTimeOffset start,
        int? compulsoryThresholdMinutes,
        string? feiId,
        string? feiRule,
        string? feiScheduleNumber
    )
        : this(
            GenerateId(),
            name,
            type,
            ruleset,
            start,
            ToTimeSpan(compulsoryThresholdMinutes),
            feiRule,
            feiId,
            feiScheduleNumber,
            [],
            []
        ) { }

    [JsonConstructor]
    public Competition(
        int? id,
        string? name,
        CompetitionType? type,
        CompetitionRuleset? ruleset,
        DateTimeOffset? start,
        TimeSpan? compulsoryThresholdSpan,
        string? feiId,
        string? feiRule,
        string? feiScheduleNumber,
        IEnumerable<Phase> phases,
        IEnumerable<Participation> participations
    )
        : base(id!.Value)
    {
        _phases = phases.ToList();
        _participations = participations.ToList();
        Name = Required(nameof(Name), name);
        Type = Required(nameof(Type), type);
        Ruleset = Required(nameof(Ruleset), ruleset);
        Start = Required(nameof(Start), start);
        CompulsoryThresholdSpan = compulsoryThresholdSpan;
        FeiId = feiId;
        FeiRule = feiRule;
        FeiScheduleNumber = feiScheduleNumber;
    }

    IReadOnlyList<Participation> IParent<Participation>.Chilren => Participations;
    IReadOnlyList<Phase> IParent<Phase>.Chilren => Phases;

    public string Name { get; }
    public CompetitionType Type { get; }
    public CompetitionRuleset Ruleset { get; }
    public DateTimeOffset Start { get; }
    public TimeSpan? CompulsoryThresholdSpan { get; }
    public string? FeiId { get; }
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

    static TimeSpan? ToTimeSpan(int? minutes)
    {
        return minutes != null ? TimeSpan.FromMinutes(minutes.Value) : null;
    }
}
