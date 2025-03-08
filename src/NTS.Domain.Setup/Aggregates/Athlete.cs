using Newtonsoft.Json;
using Not.Domain.Base;
using NTS.Domain.Aggregates;

namespace NTS.Domain.Setup.Aggregates;

public class Athlete : AggregateRoot, IAggregateRoot, IReflect<Club>
{
    public static Athlete Create(
        string? name,
        string? feiId,
        Country? country,
        Club? club,
        AthleteCategory? category
    )
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

    Athlete(
        Person? person,
        string? feiId,
        Country? country,
        Club? club,
        AthleteCategory? category
    )
        : this(GenerateId(), person, feiId, country, club, category) { }

    [System.Text.Json.Serialization.JsonConstructor]
    [JsonConstructor]
    public Athlete(
        int? id,
        Person? person,
        string? feiId,
        Country? country,
        Club? club,
        AthleteCategory? category
    )
        : base(id!.Value)
    {
        FeiId = feiId;
        Person = Required(nameof(Person), person);
        Country = Required(nameof(Country), country);
        Club = Required(nameof(Club), club);
        Category = Required(nameof(Category), category);
    }

    public string? FeiId { get; }
    public Person Person { get; }
    public Country Country { get; }
    public Club Club { get; private set; }
    public AthleteCategory Category { get; private set; }

    public override string ToString()
    {
        return Person.ToString();
    }

    public void Reflect(Club club)
    {
        Club = club;
    }
}
