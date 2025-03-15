using Newtonsoft.Json;
using Not.Domain.Base;
using Not.Localization;
using NTS.Domain.Aggregates;

namespace NTS.Domain.Core.Aggregates.Participations;

// TODO: probably shoudl be a record
public class Combination : AggregateRoot
{
    decimal _distance;

    [JsonConstructor]
    Combination(
        int id,
        int number,
        Athlete athlete,
        Horse horse,
        string distance,
        Country? country,
        Club? club,
        Speed? minAverageSpeed,
        Speed? maxAverageSpeed
    )
        : base(id)
    {
        Number = number;
        Athlete = athlete;
        Horse = horse;
        Distance = distance;
        Country = country;
        Club = club;
        MinAverageSpeed = minAverageSpeed;
        MaxAverageSpeed = maxAverageSpeed;
    }

    public Combination(
        int number,
        IAthlete athlete,
        IHorse horse,
        decimal distance,
        Country? country,
        IClub? club,
        double? minAverageSpeedlimit,
        double? maxAverageSpeedLimit
    )
        : this(
            GenerateId(),
            number,
            new Athlete(athlete),
            new Horse(horse),
            FormatDistance(distance),
            country,
            club == null ? null : new Club(club),
            Speed.Create(minAverageSpeedlimit),
            Speed.Create(maxAverageSpeedLimit)
        )
    {
        _distance = distance;
    }

    public int Number { get; }
    public Athlete Athlete { get; }
    public Horse Horse { get; }
    public Country? Country { get; }
    public Club? Club { get; }
    public Speed? MinAverageSpeed { get; }
    public Speed? MaxAverageSpeed { get; }
    public string Distance
    {
        get => FormatDistance(_distance);
        set => _distance = decimal.Parse(value);
    }

    public override string ToString()
    {
        var result = $"{"#".Localize()}{Number}: {Athlete}, {Horse}";
        var kmph = "km/h".Localize();
        if (MinAverageSpeed != null && MaxAverageSpeed != null)
        {
            return result + $" ({MinAverageSpeed}-{MaxAverageSpeed} {kmph})";
        }
        else if (MinAverageSpeed != null && MaxAverageSpeed == null)
        {
            return result + $" ({"min".Localize()}:{MinAverageSpeed} {kmph})";
        }
        else if (MinAverageSpeed == null && MaxAverageSpeed != null)
        {
            return result + $" ({"max".Localize()} : {MaxAverageSpeed}   {kmph})";
        }
        return result;
    }

    static string FormatDistance(decimal distance)
    {
        return distance.ToString("#.##");
    }
}
