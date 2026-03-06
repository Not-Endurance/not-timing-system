using Not.Domain.Krud;
using NTS.Domain.Aggregates;

namespace NTS.Domain.Setup.Aggregates;

public class Athlete : Aggregate, IEntityMirror<Club>
{
    public Athlete(Person? names, string? feiId, Country? country, Club? club, int? id = null, User? user = null)
        : base(id)
    {
        FeiId = feiId;
        Names = Required(nameof(Names), names);
        Country = Required(nameof(Country), country);
        Club = club;
        User = user;
    }

    public string? FeiId { get; }
    public Person Names { get; }
    public Country Country { get; }
    public Club? Club { get; private set; }
    public User? User { get; }

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
