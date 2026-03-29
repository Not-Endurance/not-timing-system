using Not.Domain.Krud;
using NTS.Domain.Helpers;

namespace NTS.Domain.Setup.Aggregates.UpcomingEvents;

public class Participation : Entity, IKurdMirror<Combination>
{
    const double CHILDREN_MIN_SPEED = 8;
    const double CHILDREN_MAX_SPEED = 12;
    const double MIN_SPEED = 10;
    const double MAX_SPEED = 16;

    public Participation(
        bool? isNotRanked,
        Combination? combination,
        ParticipationCategory? category,
        DateTimeOffset? startTimeOverride,
        double? maxSpeedOverride,
        double? minSpeedOverride,
        double? minAverageSpeed = null,
        double? maxAverageSpeed = null,
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
        if (minAverageSpeed.HasValue)
        {
            MinAverageSpeed = minAverageSpeed;
        }
        if (maxAverageSpeed.HasValue)
        {
            MaxAverageSpeed = maxAverageSpeed;
        }
    }

    public Combination Combination { get; private set; }
    public bool IsNotRanked { get; }
    public ParticipationCategory Category { get; }
    public DateTimeOffset? StartTimeOverride { get; }
    public double? MaxSpeedOverride { get; }
    public double? MinSpeedOverride { get; }
    public double? MinAverageSpeed { get; private set; }
    public double? MaxAverageSpeed { get; private set; }

    internal void SetSpeedLimits(CompetitionType competitionType)
    {
        MinAverageSpeed = MIN_SPEED;
        MaxAverageSpeed = null;
        if (competitionType == CompetitionType.Qualification)
        {
            if (Category == ParticipationCategory.Children)
            {
                MinAverageSpeed = CHILDREN_MIN_SPEED;
                MaxAverageSpeed = CHILDREN_MAX_SPEED;
            }
            else
            {
                MaxAverageSpeed = MAX_SPEED;
            }
        }
        if (MaxSpeedOverride != null)
        {
            MaxAverageSpeed = MaxSpeedOverride;
        }
        if (MinSpeedOverride != null)
        {
            MinAverageSpeed = MinSpeedOverride;
        }
    }

    public override string ToString()
    {
        var restrictions = ToStringHelper.FormatSpeedRestrictions(MinAverageSpeed, MaxAverageSpeed);
        var ex = IsNotRanked ? X_string : null;
        return Combine(ex, Combination, restrictions);
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
