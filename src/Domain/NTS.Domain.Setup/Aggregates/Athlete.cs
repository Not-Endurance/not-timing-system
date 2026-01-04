using Not.Domain.Aggregates;
using NTS.Domain.Aggregates;

namespace NTS.Domain.Setup.Aggregates;

public class Athlete : AggregateRoot, IAthlete, IReflect<Club>
{
    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public Athlete(int? id, Person? names, string? feiId, Country? country, Club? club)
        : base(id!.Value)
    {
        FeiId = feiId;
        Names = Required(nameof(Names), names);
        Country = Required(nameof(Country), country);
        Club = club;
    }

    public Athlete(Person? person, string? feiId, Country? country, Club? club)
        : this(GenerateId(), person, feiId, country, club) { }

    IClub? IAthlete.Club => Club;
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
