using Not.Domain.Krud;

namespace NTS.Domain.Setup.Aggregates.ConfigureEvents;

public class Combination : Entity, IKurdMirror<Athlete>, IKurdMirror<Horse>
{
    public Combination(int? number, Athlete? athlete, Horse? horse, int? id)
        : base(id)
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

    public bool Reflect(Horse child)
    {
        if (Horse != child)
        {
            return false;
        }
        Horse = child;
        return true;
    }

    public bool Reflect(Athlete child)
    {
        if (Athlete != child)
        {
            return false;
        }
        Athlete = child;
        return true;
    }
}
