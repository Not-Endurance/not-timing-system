using Newtonsoft.Json;
using Not.Domain.Base;

namespace NTS.Domain.Setup.Aggregates;

public class Combination : AggregateRoot, IParent, IReflect<Athlete>, IReflect<Horse>
{
    public static Combination Create(int? number, Athlete? athlete, Horse? horse, Tag? tag)
    {
        return new(number, athlete, horse, tag);
    }

    public static Combination Update(int? id, int? number, Athlete? athlete, Horse? horse, Tag? tag)
    {
        return new(id, number, athlete, horse, tag);
    }

    [JsonConstructor]
    public Combination(int? id, int? number, Athlete? athlete, Horse? horse, Tag? tag)
        : base(id!.Value)
    {
        Number = Required(nameof(Number), number);
        Athlete = Required(nameof(Athlete), athlete);
        Horse = Required(nameof(Horse), horse);
        Tag = tag;
    }

    public Combination(int? number, Athlete? athlete, Horse? horse, Tag? tag)
        : this(GenerateId(), number, athlete, horse, tag) { }

    public int Number { get; }
    public Athlete Athlete { get; private set; }
    public Horse Horse { get; private set; }
    public Tag? Tag { get; }

    public override string ToString()
    {
        var number = $"{"#".Localize()}{Number}";
        return Combine(number, Athlete, Horse);
    }

    public void Reflect(Horse child)
    {
        Horse = child;
    }

    public void Reflect(Athlete child)
    {
        Athlete = child;
    }
}
