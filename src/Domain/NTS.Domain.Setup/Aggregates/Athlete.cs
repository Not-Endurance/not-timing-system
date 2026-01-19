using Not.Domain.Krud;
using NTS.Domain.Aggregates;

namespace NTS.Domain.Setup.Aggregates;

public class Athlete : Aggregate, IEntityMirror<Club>
{
    public Athlete(int? id, Person? names, string? feiId, Country? country, Club? club)
        : base(id)
    {
        FeiId = feiId;
        Names = Required(nameof(Names), names);
        Country = Required(nameof(Country), country);
        Club = club;
    }

    public string? FeiId { get; }
    public Person Names { get; }
    public Country Country { get; }
    public Club? Club { get; private set; }

    public override string ToString()
    {
        return Names.ToString();
    }

    public void Reflect(Club club)
    {
        if (Club == club)
        {
            Club = club;
        }
    }
}
