using System.Globalization;
using NoTiming.Domain.Helpers;
using NoTiming.Domain.Objects;

namespace NoTiming.Domain.Core.Aggregates.Participations.Entities;

public class Combination : Entity, INoTimingDisplayable
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
        SpeedRestrictions? restrictions,
        int id
    )
        : base(id)
    {
        Number = number;
        Athlete = athlete;
        Horse = horse;
        Club = club;
        Distance = distance;
        Restrictions = restrictions ?? SpeedRestrictions.None;
    }

    public int Number { get; }
    public Athlete Athlete { get; }
    public Horse Horse { get; }
    public Club? Club { get; }
    public SpeedRestrictions Restrictions { get; }
    public string Distance
    {
        get => FormatDistance(_distance);
        init
        {
            decimal.TryParse(value, NumberFormatInfo.InvariantInfo, out var parsedValue);
            _distance = parsedValue;
        }
    }

    public string GetDisplayName(bool englishNames = false, CultureInfo? culture = null)
    {
        var speed = ToStringHelper.FormatSpeedRestrictions(
            (Speed?)Restrictions.PhaseAverageSpeedMin,
            (Speed?)Restrictions.PhaseAverageSpeedMax
        );
        return $"{hash_string}{Number}: {Athlete.GetDisplayName(englishNames, culture)}, {Horse.GetDisplayName(englishNames, culture)} {speed}";
    }

    public override string ToString()
    {
        return GetDisplayName();
    }
}
