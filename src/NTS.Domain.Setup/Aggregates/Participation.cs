using Newtonsoft.Json;
using Not.Domain.Base;

namespace NTS.Domain.Setup.Aggregates;

public class Participation : AggregateRoot, IReflect<Combination>
{
    const double CHILDREN_MIN_SPEED = 8;
    const double CHILDREN_MAX_SPEED = 12;
    const double MIN_SPEED = 10;
    const double MAX_SPEED = 16;

    [JsonConstructor]
    public Participation(
        int? id,
        DateTimeOffset? startTimeOverride,
        bool isNotRanked,
        Combination? combination,
        double? maxSpeedOverride,
        double? minAverageSpeed = null,
        double? maxAverageSpeed = null
    )
        : base(id!.Value)
    {
        StartTimeOverride = startTimeOverride;
        IsNotRanked = isNotRanked;
        Combination = Required(nameof(Combination), combination);
        MaxSpeedOverride = maxSpeedOverride;
        if (minAverageSpeed.HasValue)
        {
            MinAverageSpeed = minAverageSpeed;
        }
        if (maxAverageSpeed.HasValue)
        {
            MaxAverageSpeed = maxAverageSpeed;
        }
    }

    public Participation(
        DateTimeOffset? startTimeOverride,
        bool isNotRanked,
        Combination? combination,
        double? maxSpeedOverride
    )
        : this(GenerateId(), startTimeOverride, isNotRanked, combination, maxSpeedOverride) { }

    public Combination Combination { get; private set; }
    public bool IsNotRanked { get; }
    public DateTimeOffset? StartTimeOverride { get; }
    public double? MaxSpeedOverride { get; }
    public double? MinAverageSpeed { get; private set; }
    public double? MaxAverageSpeed { get; private set; }

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
    }

    public override string ToString()
    {
        var startTimeMessage =
            StartTimeOverride != null ? $"{start_string}: {StartTimeOverride.Value.ToLocalTime().TimeOfDay} " : null;
        var isUnrankedMessage = IsNotRanked ? not_ranked_string : null;
        return Combine(Combination, startTimeMessage, isUnrankedMessage);
    }

    public void Reflect(Combination combination)
    {
        if (Combination == combination)
        {
            Combination = combination;
        }
    }
}
