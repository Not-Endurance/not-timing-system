using Newtonsoft.Json;
using Not.Domain.Base;
using Not.Domain.Exceptions;

namespace NTS.Domain.Setup.Aggregates;

public class Competition : AggregateRoot, IParent<Participation>, IParent<Phase>
{
    public static Competition Create(
        string? name,
        CompetitionType? type,
        CompetitionRuleset ruleset,
        DateTimeOffset start,
        int? compulsoryThresholdMinutes,
        string? feiRule,
        string? feiEventCode,
        string? feiScheduleNumber,
        string? feiCategoryEventNumber
    )
    {
        return new(
            name,
            type,
            ruleset,
            start,
            compulsoryThresholdMinutes,
            feiRule,
            feiEventCode,
            feiScheduleNumber,
            feiCategoryEventNumber
        );
    }

    public static Competition Update(
        int? id,
        string? name,
        CompetitionType type,
        CompetitionRuleset? ruleset,
        DateTimeOffset start,
        int? compulsoryThresholdMinutes,
        string? feiRule,
        string? feiEventCode,
        string? feiScheduleNumber,
        string? feiCategoryEventNumber,
        IEnumerable<Phase> phases,
        IEnumerable<Participation> participations
    )
    {
        return new(
            id,
            name,
            type,
            ruleset,
            start,
            ToTimeSpan(compulsoryThresholdMinutes),
            feiRule,
            feiEventCode,
            feiScheduleNumber,
            feiCategoryEventNumber,
            phases,
            participations
        );
    }

    readonly List<Phase> _phases = [];
    readonly List<Participation> _participations = [];

    Competition(
        string? name,
        CompetitionType? type,
        CompetitionRuleset ruleset,
        DateTimeOffset start,
        int? compulsoryThresholdMinutes,
        string? feiRule,
        string? feiEventCode,
        string? feiScheduleNumber,
        string? feiCategoryEventNumber
    )
        : this(
            GenerateId(),
            name,
            type,
            ruleset,
            start,
            ToTimeSpan(compulsoryThresholdMinutes),
            feiRule,
            feiEventCode,
            feiScheduleNumber,
            feiCategoryEventNumber,
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
        string? feiRule,
        string? feiEventCode,
        string? feiScheduleNumber,
        string? feiCategoryEventNumber,
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
        FeiRule = feiRule;
        FeiEventCode = feiEventCode;
        FeiScheduleNumber = feiScheduleNumber;
        FeiCategoryEventNumber = feiCategoryEventNumber;
    }

    public string Name { get; }
    public CompetitionType Type { get; }
    public CompetitionRuleset Ruleset { get; }
    public DateTimeOffset Start { get; }
    public TimeSpan? CompulsoryThresholdSpan { get; }
    public string? FeiRule { get; }
    public string? FeiEventCode { get; }
    public string? FeiScheduleNumber { get; }
    public string? FeiCategoryEventNumber { get; }
    public IReadOnlyList<Phase> Phases => _phases.AsReadOnly();
    public IReadOnlyList<Participation> Participations => _participations.AsReadOnly();

    public override string ToString()
    {
        var type = Localize(Type);
        return Combine($"{Name} ({Phases.Count})", type, $"{Start.LocalDateTime:g}");
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
            throw new DomainPropertyException(
                nameof(Participation.Combination),
                Athletes_participating_in_Championship_Competitions_cannot_be_of_JuniorOrYoungAdult_category
            );
        }
    }

    static TimeSpan? ToTimeSpan(int? minutes)
    {
        return minutes != null ? TimeSpan.FromMinutes(minutes.Value) : null;
    }
}
