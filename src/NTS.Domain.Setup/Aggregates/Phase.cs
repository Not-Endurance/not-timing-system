using Newtonsoft.Json;
using Not.Domain.Base;
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
        : this(
            GenerateId(),
            Required(nameof(Loop), loop),
            PositiveRecovery(recovery),
            NullOrPositiveRest(rest)
        ) { }

    public Loop? Loop { get; private set; }
    public int Recovery { get; }
    public int? Rest { get; }

    public override string ToString()
    {
        var recovery = $"{Get("recovery")}: {Recovery}";
        var rest = Rest != null ? $"{Get("rest")}: {Rest}" : null;
        return Combine(Loop, recovery, rest);
    }

    public void Reflect(Loop loop)
    {
        Loop = loop;
    }

    static int PositiveRecovery(int? minutes)
    {
        if (minutes == null || minutes.Value <= 0)
        {
            throw new DomainException(nameof(Recovery), "Min value is 1 minute");
        }
        return minutes.Value;
    }

    static int? NullOrPositiveRest(int? minutes)
    {
        if (minutes <= 0)
        {
            throw new DomainException(nameof(Rest), "Min value is 1 minute");
        }
        return minutes;
    }
}
