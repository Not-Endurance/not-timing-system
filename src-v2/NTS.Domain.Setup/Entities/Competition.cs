using Newtonsoft.Json;

namespace NTS.Domain.Setup.Entities;

public class Competition : DomainEntity, ISummarizable, IParent<Participation>, IParent<Phase>
{
    public static Competition Create(
        string? name,
        CompetitionType? type,
        CompetitionRuleset ruleset,
        DateTimeOffset start,
        int? compulsoryThresholdMinutes)
        => new(name, type, ruleset, start, compulsoryThresholdMinutes);

    public static Competition Update(
        int id,
        string? name,
        CompetitionType type,
        CompetitionRuleset? ruleset,
        DateTimeOffset start,
        int? compulsoryThresholdMinutes,
        IEnumerable<Phase> phases,
        IEnumerable<Participation> participations)
        => new(id, name, type, ruleset, start, ToTimeSpan(compulsoryThresholdMinutes), phases, participations);

    readonly List<Phase> _phases = [];
    readonly List<Participation> _participations = [];

    [JsonConstructor]
    private Competition(
        int id, 
        string? name, 
        CompetitionType? type,
        CompetitionRuleset? ruleset,
        DateTimeOffset start,
        TimeSpan? compulsoryThresholdSpan,
        IEnumerable<Phase> phases,
        IEnumerable<Participation> participations) : base(id)
    {
        _phases = phases.ToList();
        _participations = participations.ToList();
        Name = Required(nameof(Name), name);
        Type = Required(nameof(Type), type);
        Ruleset = Required(nameof(Ruleset), ruleset);
        Start = start;
        CompulsoryThresholdSpan = compulsoryThresholdSpan;
    }

    private Competition(
        string? name,
        CompetitionType? type,
        CompetitionRuleset ruleset,
        DateTimeOffset start,
        int? compulsoryThresholdMinutes)
            : this(
                GenerateId(),
                name,
                type,
                ruleset,
                IsFutureTime(nameof(Start), start),
                ToTimeSpan(compulsoryThresholdMinutes),
                [],
                [])
    {
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

    public string Name { get; }
    public CompetitionType Type { get; }
    public CompetitionRuleset Ruleset { get; }
	public DateTimeOffset Start { get; }
    public TimeSpan? CompulsoryThresholdSpan { get; }
    public IReadOnlyList<Phase> Phases => _phases.AsReadOnly();
    public IReadOnlyList<Participation> Participations => _participations.AsReadOnly();

    public string Summarize()
	{
		var summary = new Summarizer(this);
		summary.Add("phases".Localize(), _phases);
		summary.Add("contestants".Localize(), _participations);
		return summary.ToString();
	}

	public override string ToString()
	{
        return Combine(
            $"{Name} ({Phases.Count})",
            Type.ToString().Localize(),
            $"{Start:g}");
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
}