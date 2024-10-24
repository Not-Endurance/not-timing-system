﻿using Newtonsoft.Json;

namespace NTS.Domain.Setup.Entities;

public class Loop : DomainEntity
{
    public static Loop Create(double distance) => new (distance);
    public static Loop Update(int id, double distance) => new(id, distance);

    [JsonConstructor]
    public Loop(int id, double distance) : base(id)
    {
        Distance = PositiveDistance(distance);
    }
    public Loop(double distance) : this(GenerateId(), distance)
    {
    }

    public double Distance { get; }

    public override string ToString() 
    {
        return $"{Distance}{"km".Localize()}";
    }

    static double PositiveDistance(double distance)
    {
        if (distance <= 0)
        {
            throw new DomainException(nameof(Distance), "Distance cannot be zero or less.");
        }
        return distance;
    }
}
