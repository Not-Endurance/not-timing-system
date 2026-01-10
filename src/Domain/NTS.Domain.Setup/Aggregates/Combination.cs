using Newtonsoft.Json;
using Not.Domain.Aggregates;

namespace NTS.Domain.Setup.Aggregates;

public class Combination : AggregateRoot, IReflect<Athlete>, IReflect<Horse>
{
    public static Combination Create(int? number, Athlete? athlete, Horse? horse, Tag? _)
    {
        return new(number, athlete, horse);
    }

    public static Combination Update(int? id, int? number, Athlete? athlete, Horse? horse, Tag? _)
    {
        return new(id, number, athlete, horse);
    }

    [JsonConstructor]
    public Combination(int? id, int? number, Athlete? athlete, Horse? horse)
        : base(id!.Value)
    {
        Number = Required(nameof(Number), number);
        Athlete = Required(nameof(Athlete), athlete);
        Horse = Required(nameof(Horse), horse);
    }

    public Combination(int? number, Athlete? athlete, Horse? horse)
        : this(GenerateId(), number, athlete, horse) { }

    public int Number { get; }
    public Athlete Athlete { get; private set; }
    public Horse Horse { get; private set; }
    public Tag? Tag { get; }

    public override string ToString()
    {
        var number = $"{hash_string}{Number}";
        return Combine(number, Athlete, Horse);
    }

    public void Reflect(Horse child)
    {
        if (Horse == child)
        {
            Horse = child;
        }
    }

    public void Reflect(Athlete child)
    {
        if (Athlete == child)
        {
            Athlete = child;
        }
    }
}
