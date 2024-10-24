using NTS.Domain.Setup.Import;
using Newtonsoft.Json;

namespace NTS.Domain.Setup.Entities;

public class Phase : DomainEntity, ISummarizable, IImportable
{
    public static Phase Create(Loop? loop, int recovery, int? rest) => new(loop, recovery, rest);

    public static Phase Update(int id, Loop? loop, int recovery, int? rest) => new(id, loop, recovery, rest);

    [JsonConstructor]
    public Phase(int id, Loop? loop, int recovery, int? rest) : base(id)
    {
        Loop = Required(nameof(Loop), loop);
        Recovery = recovery;
        Rest = rest;
    }

    public Phase(Loop? loop, int recovery, int? rest) : this(
        GenerateId(),
        Required(nameof(Loop), loop),
        PositiveRecovery(recovery),
        NullOrPositiveRest(rest))
    {
	}

    static int PositiveRecovery(int minutes)
    {
        if (minutes <= 0)
        {
            throw new DomainException(nameof(Recovery), "Min value is 1 minute");
        }
        return minutes;
    }

    static int? NullOrPositiveRest(int? minutes)
    {
        if (minutes <= 0)
        {
            throw new DomainException(nameof(Rest), "Min value is 1 minute");
        }
        return minutes;
    }

    public Loop? Loop { get; }
    public int Recovery { get; }
    public int? Rest { get; }

    public override string ToString()
    {
        var recovery = $"{Get("recovery")}: {Recovery}";
        var rest = Rest != null ? $"{Get("rest")}: {Rest}" : null;
        return Combine(Loop, recovery, rest);
    }
}
