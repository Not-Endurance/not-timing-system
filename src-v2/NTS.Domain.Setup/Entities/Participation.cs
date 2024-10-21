using Newtonsoft.Json;

namespace NTS.Domain.Setup.Entities;

public class Participation : DomainEntity, ISummarizable
{
    public static Participation Create(DateTimeOffset? newStart, bool isUnranked, Combination? combination) => new(newStart, isUnranked, combination);
    public static Participation Update(int id, DateTimeOffset? newStart, bool isUnranked, Combination? combination) => new(id, newStart, isUnranked, combination);
    
    List<Combination> _combinations = [];

    [JsonConstructor]
    private Participation(int id, DateTimeOffset? startTimeOverride, bool isUnranked, Combination? combination) : base(id)
    {
        Id = id;
        StartTimeOverride = startTimeOverride;
        IsNotRanked = isUnranked;
        Combination = Required(nameof(Combination), combination);
    }

    private Participation(DateTimeOffset? startTimeOverride, bool isUnranked, Combination? combination) : this(
        GenerateId(),
        Validate(startTimeOverride),
        isUnranked,
        combination)
    {
        throw new DomainException(nameof(IsNotRanked), "test");
    }

    static DateTimeOffset? Validate(DateTimeOffset? startTimeOverride)
    {
        if (startTimeOverride != null && startTimeOverride.Value.DateTime.CompareTo(DateTime.Today) < 0)
        {
            throw new DomainException(nameof(StartTimeOverride), "Start time cannot be in the past");
        }
        return startTimeOverride;
    }

    public Combination Combination {  get; private set; }
    public DateTimeOffset? StartTimeOverride { get; private set; }
    public bool IsNotRanked { get; private set; }

    public IReadOnlyList<Combination> Combinations
    {
        get => _combinations.AsReadOnly();
        private set => _combinations = value.ToList();
    }

    public string Summarize()
    {
        var summary = new Summarizer(this);
        return summary.ToString();
    }

    public override string ToString()
    {
        var startTimeMessage = StartTimeOverride != null 
            ? $"start: {StartTimeOverride.Value.ToLocalTime().TimeOfDay} "
            : null;
        var isUnrankedMessage = IsNotRanked
            ? "not-ranked"
            : null;
        return Combine(Combination, startTimeMessage, isUnrankedMessage);
    }

    public void Add(Combination child)
    {
        _combinations.Add(child);
    }

    public void Remove(Combination child)
    {
        _combinations.Remove(child);
    }

    public void Update(Combination child)
    {
        _combinations.Remove(child);

        Add(child);
    }
}
