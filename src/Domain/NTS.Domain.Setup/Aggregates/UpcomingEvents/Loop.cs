using Newtonsoft.Json;
using Not.Domain.Aggregates;
using Not.Domain.Exceptions;

namespace NTS.Domain.Setup.Aggregates.UpcomingEvents;

public class Loop : Entity
{
    [JsonConstructor]
    public Loop(double? distance)
        : base(distance)
    {
        Distance = PositiveDistance(distance);
    }

    public double Distance { get; }

    public override string ToString()
    {
        return $"{Distance}{km_string}";
    }

    static double PositiveDistance(double? distance)
    {
        if (distance == null || distance.Value <= 0)
        {
            throw new DomainException(Distance_cannot_be_zero_or_less_string, nameof(Distance));
        }
        return distance.Value;
    }
}
