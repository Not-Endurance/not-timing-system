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
        Person name,
        string horse,
        string distance,
        Country? country,
        Club? club,
        Speed? minAverageSpeed,
        Speed? maxAverageSpeed
    )
        : base(id)
    {
        Number = number;
        Name = name;
        Horse = horse;
        Distance = distance;
        Country = country;
        Club = club;
        MinAverageSpeed = minAverageSpeed;
        MaxAverageSpeed = maxAverageSpeed;
    }

    public Combination(
        int number,
        Person name,
        string horse,
        decimal distance,
        Country? country,
        Club? club,
        double? minAverageSpeedlimit,
        double? maxAverageSpeedLimit
    )
        : this(
            GenerateId(),
            number,
            name,
            horse,
            FormatDistance(distance),
            country,
            club,
            Speed.Create(minAverageSpeedlimit),
            Speed.Create(maxAverageSpeedLimit)
        )
    {
        _distance = distance;
    }

    public int Number { get; }
    public Person Name { get; }
    public string Horse { get; }
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
        var result = $"{"#".Localize()}{Number}: {Name}, {Horse}";
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
