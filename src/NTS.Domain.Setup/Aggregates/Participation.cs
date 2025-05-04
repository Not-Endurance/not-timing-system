using Newtonsoft.Json;
using Not.Domain.Base;
using NTS.Domain.Helpers;

namespace NTS.Domain.Setup.Aggregates;

public class Participation : AggregateRoot, IReflect<Combination>
{
    const double CHILDREN_MIN_SPEED = 8;
    const double CHILDREN_MAX_SPEED = 12;
    const double MIN_SPEED = 10;
    const double MAX_SPEED = 16;

    public static Participation Create(
        DateTimeOffset? newStart,
        bool isUnranked,
        Combination? combination,
        double? maxSpeedOverride
    )
    {
        return new(newStart, isUnranked, combination, maxSpeedOverride);
    }

    public static Participation Update(
        int? id,
        DateTimeOffset? newStart,
        bool isUnranked,
        Combination? combination,
        double? maxSpeedOverride
    )
    {
        return new(id, newStart, isUnranked, combination, maxSpeedOverride);
    }

    [JsonConstructor]
    public Participation(
        int? id,
        DateTimeOffset? startTimeOverride,
        bool isNotRanked,
        Combination? combination,
        double? maxSpeedOverride
    )
        : base(id!.Value)
    {
        StartTimeOverride = startTimeOverride;
        IsNotRanked = isNotRanked;
        Combination = Required(nameof(Combination), combination);
        MaxSpeedOverride = maxSpeedOverride;
    }

    public Participation(
        DateTimeOffset? startTimeOverride,
        bool isNotRanked,
        Combination? combination,
        double? maxSpeedOverride,
        double? minSpeedOverride
    )
        : this(GenerateId(), IsFutureTime(startTimeOverride), isUnranked, combination, maxSpeedOverride) { }

    public Combination Combination { get; private set; }
    public bool IsNotRanked { get; }
    public DateTimeOffset? StartTimeOverride { get; }
    public double? MaxSpeedOverride { get; }
    public double? MinAverageSpeed { get; private set; }
    public double? MaxAverageSpeed { get; private set; }
    public double? MaxSpeedOverride { get; private set; }

    internal void SetSpeedLimits(CompetitionType competitionType)
    {
        var athleteCategory = Combination.Athlete.Category;
        MinAverageSpeed = MIN_SPEED;
        if (competitionType == CompetitionType.Qualification)
        {
            if (athleteCategory == AthleteCategory.Children)
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
        if(MinSpeedOverride != null)
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
