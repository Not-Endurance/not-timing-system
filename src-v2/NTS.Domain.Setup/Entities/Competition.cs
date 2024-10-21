using Newtonsoft.Json;

namespace NTS.Domain.Setup.Entities;

public class Competition : DomainEntity, ISummarizable, IParent<Contestant>, IParent<Phase>
{

    public static Competition Create(string name, CompetitionRuleset ruleset, CompetitionType type, DateTimeOffset start, int? criRecovery)
        => new(name, ruleset, type, start, criRecovery);
    public static Competition Update(
        int id,
        string name,
        CompetitionRuleset ruleset,
        CompetitionType type,
        DateTimeOffset start,
        int? criRecovery,
        IEnumerable<Phase> phases,
        IEnumerable<Contestant> contestants)
        => new(id, name, ruleset, type, start, criRecovery, phases, contestants);

    private List<Phase> _phases = new();
    private List<Contestant> _contestants = new();

    [JsonConstructor]
    private Competition(int id) : base(id) { }
    private Competition(
        int id, 
        string name,
        CompetitionRuleset ruleset,
        CompetitionType type,
        DateTimeOffset startTime,
        int? criRecovery,
        IEnumerable<Phase> phases,
        IEnumerable<Contestant> contestants) : this(name, ruleset, type, startTime, criRecovery)
    {
        Id = id;
        _phases = phases.ToList();
        _contestants = contestants.ToList();
    }
    private Competition(string name, CompetitionRuleset ruleset, CompetitionType type, DateTimeOffset startTime, int? criRecovery)
    {
        if (type == default)
        {
            throw new DomainException(nameof(type), "Competition Type is required");
        }
        if (startTime.DateTime < DateTime.Today)
        {
            throw new DomainException(nameof(StartTime), "Competition date cannot be in the past");
        }

        Name = name;
        Ruleset = ruleset;
        Type = type;
        StartTime = startTime;
        CriRecovery = criRecovery;
    }

    public string Name { get; private set; }
    public CompetitionRuleset Ruleset { get; private set; }
    public CompetitionType Type { get; private set; }
	public DateTimeOffset StartTime { get; private set; }
    public int? CriRecovery { get; private set; }
    public IReadOnlyList<Phase> Phases
    {
        get => _phases.AsReadOnly();
        private set => _phases = value.ToList();
    }
    public IReadOnlyList<Contestant> Contestants
    {
        get => _contestants.AsReadOnly();
        private set => _contestants = value.ToList();
    }

    public string Summarize()
	{
		var summary = new Summarizer(this);
		summary.Add("phases".Localize(), _phases);
		summary.Add("contestants".Localize(), _contestants);
		return summary.ToString();
	}
	public override string ToString()
	{
        return Combine(
            LocalizationHelper.Get(Type),
            $"{"phases".Localize()}: {Phases.Count}",
            $"{"start".Localize()}: {StartTime:f}");
	}

    public void Add(Contestant child)
    {
        _contestants.Add(child);
    }

    public void Remove(Contestant child)
    {
        _contestants.Remove(child);
    }

    public void Update(Contestant child)
    {
        _contestants.Remove(child);
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