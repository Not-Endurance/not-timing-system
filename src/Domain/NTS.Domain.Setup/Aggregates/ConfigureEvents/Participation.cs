using Not.Domain.Krud;

namespace NTS.Domain.Setup.Aggregates.ConfigureEvents;

public class Participation : Entity, IKurdMirror<Combination>
{
    public Participation(
        bool? isNotRanked,
        Combination? combination,
        ParticipationCategory? category,
        DateTimeOffset? startTimeOverride,
        double? maxSpeedOverride,
        double? minSpeedOverride,
        int? id = null
    )
        : base(id)
    {
        StartTimeOverride = startTimeOverride;
        IsNotRanked = isNotRanked ?? false;
        Combination = Required(nameof(Combination), combination);
        Category = Required(nameof(Category), category);
        MaxSpeedOverride = maxSpeedOverride;
        MinSpeedOverride = minSpeedOverride;
    }

    public Combination Combination { get; private set; }
    public bool IsNotRanked { get; }
    public ParticipationCategory Category { get; }
    public DateTimeOffset? StartTimeOverride { get; }
    public double? MaxSpeedOverride { get; }
    public double? MinSpeedOverride { get; }

    public override string ToString()
    {
        var ex = IsNotRanked ? X_string : null;
        return Combine(ex, Combination);
    }

    public bool Reflect(Combination combination)
    {
        if (Combination != combination)
        {
            return false;
        }
        Combination = combination;
        return true;
    }
}
