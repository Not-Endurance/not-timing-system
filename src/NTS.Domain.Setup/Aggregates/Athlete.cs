using Not.Domain.Base;
using NTS.Domain.Aggregates;

namespace NTS.Domain.Setup.Aggregates;

public class Athlete : AggregateRoot, IAthlete, IReflect<Club>
{
    public static Athlete Create(string? name, string? feiId, Country? country, Club? club, AthleteCategory? category)
    {
        return new(Person.Create(name), feiId, country, club, category);
    }

    public static Athlete Update(
        int? id,
        string? name,
        string? feiId,
        Country? country,
        Club? club,
        AthleteCategory? category
    )
    {
        return new(id, Person.Create(name), feiId, country, club, category);
    }

    Athlete(Person? person, string? feiId, Country? country, Club? club, AthleteCategory? category)
        : this(GenerateId(), person, feiId, country, club, category) { }

    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public Athlete(int? id, Person? names, string? feiId, Country? country, Club? club, AthleteCategory? category)
        : base(id!.Value)
    {
        FeiId = feiId;
        Names = Required(nameof(Names), names);
        Country = Required(nameof(Country), country);
        Category = Required(nameof(Category), category);
        Club = club;
    }

    IClub? IAthlete.Club => Club;
    public string? FeiId { get; }
    public Person Names { get; }
    public Country Country { get; }
    public Club? Club { get; private set; }
    public AthleteCategory Category { get; private set; }

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
