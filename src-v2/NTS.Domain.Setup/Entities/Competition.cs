using Newtonsoft.Json;

namespace NTS.Domain.Setup.Entities;

public class Competition : DomainEntity, ISummarizable, IParent<Contestant>, IParent<Phase>
{
    public static Competition Create(string? name, CompetitionRuleset? type, DateTimeOffset start, int? criRecovery)
        => new(name, type, start, criRecovery);

    public static Competition Update(
        int id,
        string? name,
        CompetitionRuleset? type,
        DateTimeOffset start,
        int? criRecovery,
        IEnumerable<Phase> phases,
        IEnumerable<Contestant> contestants)
        => new(id, name, type, start, criRecovery, phases, contestants);

    private List<Phase> _phases = [];
    private List<Contestant> _contestants = [];

    [JsonConstructor]
    private Competition(
        int id, 
        string? name, 
        CompetitionRuleset? type,
        DateTimeOffset startTime,
        int? criRecovery,
        IEnumerable<Phase> phases,
        IEnumerable<Contestant> contestants) : base(id)
    {
        Name = Required(nameof(Name), name);
        Type = Required(nameof(Type), type);
        StartTime = startTime;
        CriRecovery = criRecovery;
        _phases = phases.ToList();
        _contestants = contestants.ToList();
    }

    private Competition(string? name, CompetitionRuleset? type, DateTimeOffset startTime, int? criRecovery) : this(
        GenerateId(),
        name,
        type,
        NotBeforeToday(nameof(StartTime), startTime),
        criRecovery,
        [],
        [])
    {
    }

    static DateTimeOffset NotBeforeToday(string field, DateTimeOffset startTime)
    {
        if (startTime.DateTime < DateTime.Today)
        {
            throw new DomainException(field, "Start day cannot be in the past");
        }
        return startTime;
    }

    public string Name { get; private set; }
    public CompetitionRuleset Type { get; private set; }
	public DateTimeOffset StartTime { get; private set; }
    public int? CriRecovery { get; private set; } //TODO: change to TimSpan
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