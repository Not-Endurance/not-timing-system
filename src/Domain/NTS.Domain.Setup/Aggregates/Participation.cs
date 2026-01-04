using Not.Domain.Aggregates;
using NTS.Domain.Helpers;

namespace NTS.Domain.Setup.Aggregates;

public class Participation : AggregateRoot, IReflect<Combination>
{
    const double CHILDREN_MIN_SPEED = 8;
    const double CHILDREN_MAX_SPEED = 12;
    const double MIN_SPEED = 10;
    const double MAX_SPEED = 16;

    public Participation(
        int? id,
        bool isNotRanked,
        Combination? combination,
        ParticipationCategory? category,
        DateTimeOffset? startTimeOverride,
        double? maxSpeedOverride,
        double? minSpeedOverride,
        double? minAverageSpeed = null,
        double? maxAverageSpeed = null
    )
        : base(id!.Value)
    {
        StartTimeOverride = startTimeOverride;
        IsNotRanked = isNotRanked;
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

    public void Reflect(Combination combination)
    {
        if (Combination == combination)
        {
            Combination = combination;
        }
    }
}
