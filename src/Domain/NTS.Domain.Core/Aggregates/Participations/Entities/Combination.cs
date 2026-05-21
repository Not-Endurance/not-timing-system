using System.Globalization;
using NTS.Domain.Helpers;

namespace NTS.Domain.Core.Aggregates.Participations.Entities;

public class Combination : Entity, INtsDisplayable
{
    public static string FormatDistance(decimal distance)
    {
        return distance.ToString("#.##");
    }

    decimal _distance;

    public Combination(
        int number,
        Athlete athlete,
        Horse horse,
        Club? club,
        string distance,
        Speed? minAverageSpeed,
        Speed? maxAverageSpeed,
        int id
    )
        : base(id)
    {
        Number = number;
        Athlete = athlete;
        Horse = horse;
        Club = club;
        Distance = distance;
        MinAverageSpeed = minAverageSpeed;
        MaxAverageSpeed = maxAverageSpeed;
    }

    public int Number { get; }
    public Athlete Athlete { get; }
    public Horse Horse { get; }
    public Club? Club { get; }
    public Speed? MinAverageSpeed { get; }
    public Speed? MaxAverageSpeed { get; }
    public string Distance
    {
        get => FormatDistance(_distance);
        init
        {
            decimal.TryParse(value, NumberFormatInfo.InvariantInfo, out var parsedValue);
            _distance = parsedValue;
        }
    }

    public string GetDisplayName(CompetitionRuleset? ruleset = null, CultureInfo? culture = null)
    {
        var speed = ToStringHelper.FormatSpeedRestrictions(MinAverageSpeed, MaxAverageSpeed);
        return $"{hash_string}{Number}: {Athlete.GetDisplayName(ruleset, culture)}, {Horse.GetDisplayName(ruleset, culture)} {speed}";
    }

    public override string ToString()
    {
        return GetDisplayName();
    }
}
