using Newtonsoft.Json;
using Not.Domain.Aggregates;
using Not.Domain.Exceptions;

namespace NTS.Domain.Setup.Aggregates;

public class Phase : AggregateRoot, IAggregateRoot, IReflect<Loop>
{
    public static Phase Create(Loop? loop, int? recovery, int? rest)
    {
        return new(loop, recovery, rest);
    }

    public static Phase Update(int? id, Loop? loop, int? recovery, int? rest)
    {
        return new(id, loop, recovery, rest);
    }

    [JsonConstructor]
    public Phase(int? id, Loop? loop, int? recovery, int? rest)
        : base(id!.Value)
    {
        Loop = Required(nameof(Loop), loop);
        Recovery = Required(nameof(Recovery), recovery);
        Rest = rest;
    }

    public Phase(Loop? loop, int? recovery, int? rest)
        : this(GenerateId(), Required(nameof(Loop), loop), PositiveRecovery(recovery), NullOrPositiveRest(rest)) { }

    public Loop? Loop { get; private set; } // TODO: shouldnt be nullable probably
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
