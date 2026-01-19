using Newtonsoft.Json;
using Not.Domain.Exceptions;
using Not.Domain.Krud;
using Not.Structures;

namespace NTS.Domain.Setup.Aggregates.UpcomingEvents;

public class Phase : Entity, IEntityMirror<Loop>, IIdentifiable
{
    [JsonConstructor]
    public Phase(int? id, Loop? loop, int? recovery, int? rest)
        : base(id)
    {
        Id = Required(nameof(id), id);
        Loop = Required(nameof(Loop), loop);
        Recovery = PositiveRecovery(recovery);
        Rest = NullOrPositiveRest(rest);
    }

    public int Id { get; }
    public Loop Loop { get; private set; }
    public int Recovery { get; }
    public int? Rest { get; }

    public override string ToString()
    {
        var recovery = $"{Recovery_string}: {Recovery}";
        var rest = Rest != null ? $"{rest_string}: {Rest}" : null;
        return Combine(Loop, recovery, rest);
    }

    public void Reflect(Loop loop)
    {
        if (Loop == loop)
        {
            Loop = loop;
        }
    }

    static int PositiveRecovery(int? minutes)
    {
        if (minutes is not > 0)
        {
            throw new DomainPropertyException(nameof(Recovery), Min_value_is_1_minute_string);
        }
        return minutes.Value;
    }

    static int? NullOrPositiveRest(int? minutes)
    {
        if (minutes <= 0)
        {
            throw new DomainPropertyException(nameof(Rest), Min_value_is_1_minute_string);
        }
        return minutes;
    }
}
