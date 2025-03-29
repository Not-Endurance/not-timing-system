using Newtonsoft.Json;
using Not.Domain.Base;
using Not.Domain.Exceptions;

namespace NTS.Domain.Setup.Aggregates;

public class Competition : AggregateRoot, IParent<Participation>, IParent<Phase>, IAggregateRoot
{
    public static Competition Create(
        string? name,
        CompetitionType? type,
        CompetitionRuleset ruleset,
        DateTimeOffset start,
        int? compulsoryThresholdMinutes
    )
    {
        return new(name, type, ruleset, start, compulsoryThresholdMinutes);
    }

    public static Competition Update(
        int? id,
        string? name,
        CompetitionType type,
        CompetitionRuleset? ruleset,
        DateTimeOffset start,
        int? compulsoryThresholdMinutes,
        IEnumerable<Phase> phases,
        IEnumerable<Participation> participations
    )
    {
        return new(id, name, type, ruleset, start, ToTimeSpan(compulsoryThresholdMinutes), phases, participations);
    }

    readonly List<Phase> _phases = [];
    readonly List<Participation> _participations = [];

    [JsonConstructor]
    Competition(
        int? id,
        string? name,
        CompetitionType? type,
        CompetitionRuleset? ruleset,
        DateTimeOffset start,
        TimeSpan? compulsoryThresholdSpan,
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
        Start = start;
        CompulsoryThresholdSpan = compulsoryThresholdSpan;
    }

    Competition(
        string? name,
        CompetitionType? type,
        CompetitionRuleset ruleset,
        DateTimeOffset start,
        int? compulsoryThresholdMinutes
    )
        : this(
            GenerateId(),
            name,
            type,
            ruleset,
            IsFutureTime(nameof(Start), start),
            ToTimeSpan(compulsoryThresholdMinutes),
            [],
            []
        ) { }

    public string Name { get; }
    public CompetitionType Type { get; }
    public CompetitionRuleset Ruleset { get; }
    public DateTimeOffset Start { get; }
    public TimeSpan? CompulsoryThresholdSpan { get; }
    public IReadOnlyList<Phase> Phases => _phases.AsReadOnly();
    public IReadOnlyList<Participation> Participations => _participations.AsReadOnly();

    public override string ToString()
    {
        var type = Localize(Type);
        return Combine($"{Name} ({Phases.Count})", type, $"{Start:g}");
    }

    public void Add(Participation child)
    {
        ValidateAthleteCategory(child);
        child.SetSpeedLimits(Type);
        _participations.Add(child);
    }

    public void Remove(Participation child)
    {
        _participations.Remove(child);
    }

    public void Update(Participation child)
    {
        ValidateAthleteCategory(child);
        _participations.Remove(child);
        Add(child);
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
        _phases.Remove(child);
        Add(child);
    }

    void ValidateAthleteCategory(Participation child)
    {
        if (
            child.Combination.Athlete.Category == AthleteCategory.JuniorOrYoungAdult
            && Type == CompetitionType.Championship
        )
        {
            throw new DomainException(
                nameof(Participation.Combination),
                "Athletes participating in Championship Competitions cannot be of JuniorOrYoungAdult category."
            );
        }
    }

    static DateTimeOffset IsFutureTime(string field, DateTimeOffset start)
    {
        if (start <= DateTimeOffset.Now)
        {
            throw new DomainException(field, "Competition start cannot be in the past");
        }
        return start;
    }

    static TimeSpan? ToTimeSpan(int? minutes)
    {
        return minutes != null ? TimeSpan.FromMinutes(minutes.Value) : null;
    }
}
