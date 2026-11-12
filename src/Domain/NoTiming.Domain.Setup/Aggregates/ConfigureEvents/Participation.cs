using NC.Domain.Krud;
using NoTiming.Domain.Objects;

namespace NoTiming.Domain.Setup.Aggregates.ConfigureEvents;

public class Participation : Entity, IKurdMirror<Combination>
{
    public Participation(
        bool? isNotRanked,
        Combination? combination,
        ParticipationCategory? category,
        DateTimeOffset? startTimeOverride,
        SpeedRestrictions? restrictions,
        int? id = null,
        int? storedDomainVersion = null
    )
        : base(id, storedDomainVersion: storedDomainVersion)
    {
        StartTimeOverride = startTimeOverride;
        IsNotRanked = isNotRanked ?? false;
        Combination = Required(nameof(Combination), combination);
        Category = Required(nameof(Category), category);
        Restrictions = restrictions ?? SpeedRestrictions.None;
    }

    public Combination Combination { get; private set; }
    public bool IsNotRanked { get; }
    public ParticipationCategory Category { get; }
    public DateTimeOffset? StartTimeOverride { get; }
    public SpeedRestrictions Restrictions { get; }

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
