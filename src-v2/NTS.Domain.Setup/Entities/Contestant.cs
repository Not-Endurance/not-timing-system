using Newtonsoft.Json;

namespace NTS.Domain.Setup.Entities;
public class Contestant : DomainEntity, ISummarizable
{
    public static Contestant Create(DateTimeOffset? newStart, bool isUnranked, Combination combination, double? maxSpeedOverride) => new(newStart, isUnranked, combination, maxSpeedOverride);
    public static Contestant Update(int id, DateTimeOffset? newStart, bool isUnranked, Combination combination, double? maxSpeedOverride) => new(id, newStart, isUnranked, combination, maxSpeedOverride);
    private List<Combination> _combinations = new();

    [JsonConstructor]
    private Contestant(int id, DateTimeOffset? startTimeOverride, bool isUnranked, Combination combination, double? maxSpeedOverride)
    {
        Id = id;
        StartTimeOverride = startTimeOverride;
        MaxSpeedOverride = maxSpeedOverride;
        IsUnranked = isUnranked;
        Combination = combination;
    }
    // TODO: use this ctor to validate and then call the JsonCtor to assign the property values
    // then remove private setter where not needed as serialization happens through the ctor anyway
    // Move the ID generation logic in protected method in DomainEntity and call inside this() to generate ID
    // as oposed to generating it automatically in DomainEntity ctor
    private Contestant(DateTimeOffset? startTimeOverride, bool isUnranked, Combination combination, double? maxSpeedOverride)
    {
        if (startTimeOverride != null && startTimeOverride.Value.DateTime.CompareTo(DateTime.Today) < 0)
        {
            throw new DomainException(nameof(StartTimeOverride), "Start time cannot be in the past");
        }
        StartTimeOverride = startTimeOverride;
        MaxSpeedOverride = maxSpeedOverride;
        IsUnranked = isUnranked;
        Combination = combination;
    }

    public Combination Combination {  get; private set; }
    public DateTimeOffset? StartTimeOverride { get; private set; }
    public double? MaxSpeedOverride { get; private set; }
    public bool IsUnranked { get; private set; }

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
        var isUnrankedMessage = IsUnranked
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
