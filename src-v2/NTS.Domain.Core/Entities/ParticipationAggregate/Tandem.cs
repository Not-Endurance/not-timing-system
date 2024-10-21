﻿using Not.Localization;

namespace NTS.Domain.Core.Entities.ParticipationAggregate;

// TODO: probably shoudl be a record
public class Tandem : DomainEntity
{
    private decimal _distance;

    private Tandem(
        int id,
        int number,
        Person name,
        string horse,
        string distance,
        Country? country,
        Club? club,
        Speed? minSpeedLimit,
        Speed? maxSpeedLimit) : base(id)
    {
        Number = number;
        Name = name;
        Horse = horse;
        Distance = distance;
        Country = country;
        Club = club;
        MinAverageSpeed = minSpeedLimit;
        MaxAverageSpeed = maxSpeedLimit;

    }
    public Tandem(
        int number,
        Person name,
        string horse,
        decimal distance,
        Country? country,
        Club? club,
        double? minAverageSpeedlimit,
        double? maxAverageSpeedLimit) : this(
            GenerateId(),
            number,
            name,
            horse,
            FormatDistance(distance),
            country,
            club,
            Speed.Create(minAverageSpeedlimit),
            Speed.Create(maxAverageSpeedLimit))
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
        var message = $"{"#".Localize()}{Number}: {Name}, {Horse}";
        var kmph = "km/h".Localize();
        if (MinAverageSpeed != null && MaxAverageSpeed != null)
        {
            return message + $" ({MinAverageSpeed}-{MaxAverageSpeed} {kmph})";
        }
        else if (MinAverageSpeed != null)
        {
            return message + $" ({"min".Localize()}:{MinAverageSpeed} {kmph})";
        }
        else
        {
            return message + $" ({"max".Localize()} : {MaxAverageSpeed}   {kmph})";
        }
    }

    static string FormatDistance(decimal distance)
    {
        return distance.ToString("#.##");
    }
}
