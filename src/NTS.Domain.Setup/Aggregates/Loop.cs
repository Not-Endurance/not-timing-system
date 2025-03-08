using Newtonsoft.Json;
using Not.Domain.Base;
using Not.Domain.Exceptions;

namespace NTS.Domain.Setup.Aggregates;

public class Loop : AggregateRoot, IAggregateRoot
{
    public static Loop Create(double? distance)
    {
        return new(distance);
    }

    public static Loop Update(int? id, double? distance)
    {
        return new(id, distance);
    }

    [JsonConstructor]
    public Loop(int? id, double? distance)
        : base(id!.Value)
    {
        Distance = PositiveDistance(distance);
    }

    public Loop(double? distance)
        : this(GenerateId(), distance) { }

    public double Distance { get; }

    public override string ToString()
    {
        return $"{Distance}{"km".Localize()}";
    }

    static double PositiveDistance(double? distance)
    {
        if (distance == null || distance.Value <= 0)
        {
            throw new DomainException(nameof(Distance), "Distance cannot be zero or less.");
        }
        return distance.Value;
    }
}
