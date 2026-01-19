using Newtonsoft.Json;
using Not.Domain.Krud;

namespace NTS.Domain.Setup.Aggregates.UpcomingEvents;

public class Combination : Entity, IEntityMirror<Athlete>, IEntityMirror<Horse>
{
    [JsonConstructor]
    public Combination(int? number, Athlete? athlete, Horse? horse)
        : base(number)
    {
        Number = Required(nameof(Number), number);
        Athlete = Required(nameof(Athlete), athlete);
        Horse = Required(nameof(Horse), horse);
    }

    public int Number { get; }
    public Athlete Athlete { get; private set; }
    public Horse Horse { get; private set; }

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
