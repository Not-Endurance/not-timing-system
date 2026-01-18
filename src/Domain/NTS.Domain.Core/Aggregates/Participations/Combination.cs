using System.Globalization;
using Not.Domain.Aggregates;
using NTS.Domain.Aggregates;
using NTS.Domain.Helpers;

namespace NTS.Domain.Core.Aggregates.Participations;

// TODO: probably shoudl be a record
public class Combination : Aggregate
{
    decimal _distance;

    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public Combination(
        int id,
        int number,
        Athlete athlete,
        Horse horse,
        IClub? club,
        string distance,
        Speed? minAverageSpeed,
        Speed? maxAverageSpeed
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

    public Combination(
        int number,
        IAthlete athlete,
        IHorse horse,
        decimal distance,
        double? minAverageSpeedlimit,
        double? maxAverageSpeedLimit
    )
        : this(
            GenerateId(),
            number,
            new Athlete(athlete),
            new Horse(horse),
            athlete.Club,
            FormatDistance(distance),
            minAverageSpeedlimit,
            maxAverageSpeedLimit
        )
    {
        _distance = distance;
    }

    public int Number { get; }
    public Athlete Athlete { get; }
    public Horse Horse { get; }
    public IClub? Club { get; }
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

    public override string ToString()
    {
        var speed = ToStringHelper.FormatSpeedRestrictions(MinAverageSpeed, MaxAverageSpeed);
        return $"{hash_string}{Number}: {Athlete}, {Horse} {speed}";
    }

    static string FormatDistance(decimal distance)
    {
        return distance.ToString("#.##");
    }
}
