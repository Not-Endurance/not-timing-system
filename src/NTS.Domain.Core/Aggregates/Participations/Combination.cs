using System.Globalization;
using Not.Domain.Base;
using NTS.Domain.Aggregates;

namespace NTS.Domain.Core.Aggregates.Participations;

// TODO: probably shoudl be a record
public class Combination : AggregateRoot
{
    decimal _distance;

    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public Combination(
        int id,
        int number,
        Athlete athlete,
        Horse horse,
        string distance,
        Speed? minAverageSpeed,
        Speed? maxAverageSpeed
    )
        : base(id)
    {
        Number = number;
        Athlete = athlete;
        Horse = horse;
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
            FormatDistance(distance),
            Speed.Create(minAverageSpeedlimit),
            Speed.Create(maxAverageSpeedLimit)
        )
    {
        _distance = distance;
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
        set
        {
            decimal.TryParse(value, NumberFormatInfo.InvariantInfo, out var parsedValue);
            _distance = parsedValue;
        }
    }

    public override string ToString()
    {
        var result = $"{hash_string}{Number}: {Athlete}, {Horse}";
        if (MinAverageSpeed != null && MaxAverageSpeed != null)
        {
            return result + $" ({MinAverageSpeed}-{MaxAverageSpeed} {km_per_hour_string})";
        }
        else if (MinAverageSpeed != null && MaxAverageSpeed == null)
        {
            return result + $" ({min_string}:{MinAverageSpeed} {km_per_hour_string})";
        }
        else if (MinAverageSpeed == null && MaxAverageSpeed != null)
        {
            return result + $" ({max_string} : {MaxAverageSpeed}   {km_per_hour_string})";
        }
        return result;
    }

    static string FormatDistance(decimal distance)
    {
        return distance.ToString("#.##");
    }
}
